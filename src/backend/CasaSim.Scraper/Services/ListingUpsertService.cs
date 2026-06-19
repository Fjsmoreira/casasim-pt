using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Core.Models;
using CasaSim.Scraper.Diagnostics;
using System.Diagnostics;
using System.Linq;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Serilog.Context;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Upserts parsed Property objects into the database as Listing entities.
/// Matches on (AgencyId, ExternalId) — the unique constraint defined in AppDbContext.
/// Maintains FirstSeenAt on create and updates LastSeenAt on every touch.
/// Replaces images safely (delete old, insert new) to avoid stale references.
/// </summary>
public sealed class ListingUpsertService
{
    private static readonly Dictionary<string, string> AgencySlugMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Remax"] = "remax-pombal",
        ["Century21"] = "century21-pombal",
        ["ERA"] = "era-pombal",
        ["Valorfin Imóveis"] = "valorfin-imoveis",
        ["Argilipe"] = "argilipe",
        ["ImoPombal"] = "imopombal",
        ["LionsCastles"] = "lionscastles",
        ["Habifit"] = "habifit",
        ["Cosy Imobiliária"] = "cosy-imobiliaria",
        ["Moderno Imóveis"] = "moderno-imoveis",
        ["Neves & Terlouw"] = "neves-terlouw",
        ["Veigas"] = "veigas",
        ["Zome"] = "zome",
    };

    /// <summary>
    /// Freguesias and localities in the Pombal concelho (município).
    /// Used to filter out listings from outside the target area.
    /// </summary>
    private static readonly HashSet<string> PombalConcelhoKeywords = new(StringComparer.OrdinalIgnoreCase)
    {
        // Freguesias of Pombal concelho
        "Abiul",
        "Albergaria dos Doze",
        "Carnide",
        "Carriço",
        "Guia",
        "Ilha",
        "Louriçal",
        "Mata Mourisca",
        "Meirinhas",
        "Pelariga",
        "Pombal",
        "Redinha",
        "Santiago de Litém",
        "São Simão de Litém",
        "Vermoil",
        "Vila Cã",
        // Common abbreviations / variants
        "S. Simão",
        "S.Simão",
        "Santiago Litém",
    };

    private static bool IsInPombalConcelho(Property property)
    {
        // Combine all text fields from the property for keyword matching.
        var text = string.Join(" ",
            property.Title ?? "",
            property.Description ?? "",
            property.Address ?? "",
            property.City ?? "",
            property.District ?? "");

        return PombalConcelhoKeywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private readonly AppDbContext _db;
    private readonly ILogger<ListingUpsertService> _logger;
    private readonly HashSet<string> _seenAgencies = new(StringComparer.OrdinalIgnoreCase);

    public ListingUpsertService(AppDbContext db, ILogger<ListingUpsertService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Upsert a single parsed Property.
    /// Returns the tracking handle for the listing (created or updated).
    /// </summary>
    public async Task<ListingUpsertResult> UpsertAsync(
        Property property,
        string agencySlug,
        Guid? scrapeLogId = null,
        CancellationToken ct = default)
    {
        using var agencySlugScope = LogContext.PushProperty("agencySlug", agencySlug);
        using var sourceIdScope = LogContext.PushProperty("sourceId", property.ExternalId);

        // ── Resolve agency ────────────────────────────────────
        var agency = await _db.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == agencySlug, ct);

        if (agency is null)
        {
            _logger.LogWarning("Agency slug '{Slug}' not found in DB; skipping listing '{Id}'",
                agencySlug, property.ExternalId);
            return new ListingUpsertResult(ListingUpsertAction.Skipped, Guid.Empty);
        }

        // ── Pombal-area location filter ───────────────────────
        if (!IsInPombalConcelho(property))
        {
            _logger.LogDebug("Listing '{Id}' ({Title}) is outside Pombal concelho; skipping",
                property.ExternalId, property.Title);
            return new ListingUpsertResult(ListingUpsertAction.Skipped, Guid.Empty);
        }

        // ── Look up existing listing by (AgencyId, ExternalId) ──
        var existing = await _db.Listings
            .AsTracking()
            .FirstOrDefaultAsync(
                l => l.AgencyId == agency.Id && l.ExternalId == property.ExternalId,
                ct);

        if (existing is not null)
        {
            var changes = BuildChangeSummary(existing, property);

            // ── Update ────────────────────────────────────────
            MapPropertyToListing(property, existing, agency.Id);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.LastSeenAt = DateTimeOffset.UtcNow;

            // Replace images with set-based deletes instead of clearing a tracked
            // collection.  Clearing a loaded collection in large batches caused
            // Npgsql/EF to emit DELETE commands that sometimes affected 0 rows and
            // tripped DbUpdateConcurrencyException on the live scraper.
            await ReplaceImagesAsync(existing.Id, property.Images, ct);

            using var listingScope = LogContext.PushProperty("listingId", existing.Id);
            _logger.LogDebug("Updated listing {SourceId} ({Title})",
                property.ExternalId, property.Title);

            AddListingChange(
                scrapeLogId,
                ScrapeListingChangeAction.Updated,
                agencySlug,
                existing.Id,
                property.ExternalId,
                property.Title,
                property.ListingUrl,
                changes);

            return new ListingUpsertResult(ListingUpsertAction.Updated, existing.Id);
        }
        else
        {
            // ── Create ────────────────────────────────────────
            var listing = MapPropertyToListing(property, null, agency.Id);
            listing.Id = Guid.NewGuid();
            listing.CreatedAt = DateTimeOffset.UtcNow;
            listing.UpdatedAt = DateTimeOffset.UtcNow;
            listing.FirstSeenAt = DateTimeOffset.UtcNow;
            listing.LastSeenAt = DateTimeOffset.UtcNow;

            // Build images
            for (var i = 0; i < property.Images.Count; i++)
            {
                listing.Images.Add(new ListingImage
                {
                    Id = Guid.NewGuid(),
                    ListingId = listing.Id,
                    Url = property.Images[i],
                    SortOrder = i,
                    IsPrimary = i == 0,
                    CreatedAt = DateTimeOffset.UtcNow,
                    UpdatedAt = DateTimeOffset.UtcNow,
                    FirstSeenAt = DateTimeOffset.UtcNow,
                    LastSeenAt = DateTimeOffset.UtcNow,
                });
            }

            _db.Listings.Add(listing);
            using var listingScope = LogContext.PushProperty("listingId", listing.Id);
            _logger.LogDebug("Created listing {SourceId} ({Title})",
                property.ExternalId, property.Title);

            AddListingChange(
                scrapeLogId,
                ScrapeListingChangeAction.Created,
                agencySlug,
                listing.Id,
                property.ExternalId,
                property.Title,
                property.ListingUrl,
                null);

            return new ListingUpsertResult(ListingUpsertAction.Created, listing.Id);
        }
    }

    /// <summary>
    /// Upsert a batch of parsed Properties, all from the same agency.
    /// Calls SaveChangesAsync once at the end for transactional consistency.
    /// </summary>
    public async Task<BatchUpsertResult> UpsertBatchAsync(
        IReadOnlyList<Property> properties,
        string agencySlug,
        Guid? scrapeLogId = null,
        CancellationToken ct = default)
    {
        using var agencySlugScope = LogContext.PushProperty("agencySlug", agencySlug);
        using var activity = ScraperDiagnostics.ActivitySource.StartActivity(
            ScraperDiagnostics.SpanUpsertBatch,
            ActivityKind.Internal);

        activity?.SetTag(ScraperDiagnostics.TagAgencySlug, agencySlug);
        activity?.SetTag("listing_count", properties.Count);

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var property in properties)
        {
            var result = await UpsertAsync(property, agencySlug, scrapeLogId, ct);
            switch (result.Action)
            {
                case ListingUpsertAction.Created:
                    created++;
                    break;
                case ListingUpsertAction.Updated:
                    updated++;
                    break;
                default:
                    skipped++;
                    break;
            }
        }

        // Retry SaveChangesAsync on concurrency exceptions.  EF Core
        // throws DbUpdateConcurrencyException when an UPDATE or DELETE
        // affects 0 rows — this can happen spuriously in batch scenarios
        // when Npgsql's batching interacts with cascade-delete image
        // replacement.  Clear the change tracker and retry once.
        var maxRetries = 3;
        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                await _db.SaveChangesAsync(ct);
                break; // success
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateConcurrencyException) when (attempt < maxRetries)
            {
                _logger.LogWarning(
                    "Concurrency conflict saving batch for {Slug} (attempt {Attempt}/{Max}); retrying",
                    agencySlug, attempt, maxRetries);
                _db.ChangeTracker.Clear();
                // Re-process all properties from scratch — fresh reads
                // from the DB avoid stale change-tracker state.
                created = 0;
                updated = 0;
                skipped = 0;
                foreach (var property in properties)
                {
                    var result = await UpsertAsync(property, agencySlug, scrapeLogId, ct);
                    switch (result.Action)
                    {
                        case ListingUpsertAction.Created: created++; break;
                        case ListingUpsertAction.Updated: updated++; break;
                        default: skipped++; break;
                    }
                }
            }
        }

        _logger.LogInformation(
            "Batch upsert for {Slug}: {Created} created, {Updated} updated, {Skipped} skipped",
            agencySlug, created, updated, skipped);

        activity?.SetTag("result.created", created);
        activity?.SetTag("result.updated", updated);
        activity?.SetTag("result.skipped", skipped);

        return new BatchUpsertResult(created, updated, skipped);
    }

    public async Task<int> MarkMissingListingsRemovedAsync(
        string agencySlug,
        IReadOnlySet<string> seenExternalIds,
        Guid? scrapeLogId = null,
        CancellationToken ct = default)
    {
        var agency = await _db.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == agencySlug, ct);

        if (agency is null)
            return 0;

        var missing = await _db.Listings
            .AsTracking()
            .Where(l => l.AgencyId == agency.Id)
            .Where(l => l.Status != ListingStatus.Removed && l.Status != ListingStatus.Archived)
            .ToListAsync(ct);

        missing = missing
            .Where(l => !seenExternalIds.Contains(l.ExternalId))
            .ToList();

        var now = DateTimeOffset.UtcNow;
        foreach (var listing in missing)
        {
            var previousStatus = listing.Status;
            listing.Status = ListingStatus.Removed;
            listing.RemovedAt = now;
            listing.UpdatedAt = now;

            AddListingChange(
                scrapeLogId,
                ScrapeListingChangeAction.Removed,
                agencySlug,
                listing.Id,
                listing.ExternalId,
                listing.Title,
                listing.SourceUrl,
                new Dictionary<string, FieldChange?>
                {
                    ["status"] = new(previousStatus.ToString(), ListingStatus.Removed.ToString()),
                    ["removedAt"] = new(null, now.ToString("O")),
                });
        }

        return missing.Count;
    }

    // ── Mapping helpers ──────────────────────────────────────

    private static Listing MapPropertyToListing(
        Property property,
        Listing? target,
        Guid agencyId)
    {
        var listing = target ?? new Listing();
        listing.AgencyId = agencyId;
        listing.ExternalId = property.ExternalId;
        listing.SourceUrl = property.ListingUrl ?? string.Empty;
        listing.Title = property.Title;
        listing.Description = property.Description;
        listing.City = property.City;

        listing.PropertyType = MapPropertyType(property.Type);
        listing.PriceType = MapPriceType(property.Transaction);
        listing.Status = MapStatus(property.Status);

        listing.Price = property.Price;
        listing.AreaM2 = property.AreaM2 is not null ? (decimal?)property.AreaM2 : null;
        listing.LandAreaM2 = property.LandAreaM2 is not null ? (decimal?)property.LandAreaM2 : null;
        listing.Bedrooms = property.Bedrooms;
        listing.Bathrooms = property.Bathrooms;
        listing.ParkingSpaces = property.ParkingSpots;
        listing.YearBuilt = property.YearBuilt;
        listing.EnergyClass = property.EnergyClass;
        listing.PublishedAt = ToDateTimeOffset(property.PublishedAt) ?? listing.PublishedAt;

        if (property.Location is not null)
        {
            // We store the point directly — LocationId FK can be null
            // for listings without a dedicated Location entity.
        }

        return listing;
    }

    private void AddListingChange(
        Guid? scrapeLogId,
        ScrapeListingChangeAction action,
        string agencySlug,
        Guid? listingId,
        string externalId,
        string? title,
        string? sourceUrl,
        IReadOnlyDictionary<string, FieldChange?>? changes)
    {
        if (scrapeLogId is null)
            return;

        _db.ScrapeListingChanges.Add(new ScrapeListingChange
        {
            Id = Guid.NewGuid(),
            ScrapeLogId = scrapeLogId.Value,
            ListingId = listingId,
            Action = action,
            AgencySlug = agencySlug,
            ExternalId = externalId,
            Title = title,
            SourceUrl = sourceUrl,
            ChangeSummaryJson = changes is { Count: > 0 }
                ? JsonSerializer.Serialize(changes)
                : null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    private static IReadOnlyDictionary<string, FieldChange?> BuildChangeSummary(Listing existing, Property property)
    {
        var changes = new Dictionary<string, FieldChange?>();

        AddIfChanged(changes, "title", existing.Title, property.Title);
        AddIfChanged(changes, "description", existing.Description, property.Description);
        AddIfChanged(changes, "sourceUrl", existing.SourceUrl, property.ListingUrl ?? string.Empty);
        AddIfChanged(changes, "city", existing.City, property.City);
        AddIfChanged(changes, "propertyType", existing.PropertyType.ToString(), MapPropertyType(property.Type).ToString());
        AddIfChanged(changes, "priceType", existing.PriceType.ToString(), MapPriceType(property.Transaction).ToString());
        AddIfChanged(changes, "status", existing.Status.ToString(), MapStatus(property.Status).ToString());
        AddIfChanged(changes, "price", existing.Price, property.Price);
        AddIfChanged(changes, "areaM2", existing.AreaM2, property.AreaM2 is not null ? (decimal?)property.AreaM2 : null);
        AddIfChanged(changes, "landAreaM2", existing.LandAreaM2, property.LandAreaM2 is not null ? (decimal?)property.LandAreaM2 : null);
        AddIfChanged(changes, "bedrooms", existing.Bedrooms, property.Bedrooms);
        AddIfChanged(changes, "bathrooms", existing.Bathrooms, property.Bathrooms);
        AddIfChanged(changes, "parkingSpaces", existing.ParkingSpaces, property.ParkingSpots);
        AddIfChanged(changes, "yearBuilt", existing.YearBuilt, property.YearBuilt);
        AddIfChanged(changes, "energyClass", existing.EnergyClass, property.EnergyClass);

        return changes;
    }

    private static void AddIfChanged<T>(
        IDictionary<string, FieldChange?> changes,
        string field,
        T? before,
        T? after)
    {
        if (EqualityComparer<T?>.Default.Equals(before, after))
            return;

        changes[field] = new FieldChange(
            before?.ToString(),
            after?.ToString());
    }

    private async Task ReplaceImagesAsync(Guid listingId, List<string> imageUrls, CancellationToken ct)
    {
        // Remove old images without loading/tracking them. ExecuteDeleteAsync is
        // available for the live relational provider; the fallback keeps unit tests
        // using non-relational providers working.
        if (_db.Database.IsRelational())
        {
            await _db.ListingImages
                .Where(i => i.ListingId == listingId)
                .ExecuteDeleteAsync(ct);
        }
        else
        {
            var oldImages = await _db.ListingImages
                .Where(i => i.ListingId == listingId)
                .ToListAsync(ct);
            _db.ListingImages.RemoveRange(oldImages);
        }

        for (var i = 0; i < imageUrls.Count; i++)
        {
            _db.ListingImages.Add(new ListingImage
            {
                Id = Guid.NewGuid(),
                ListingId = listingId,
                Url = imageUrls[i],
                SortOrder = i,
                IsPrimary = i == 0,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow,
                FirstSeenAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            });
        }
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        if (value is null)
            return null;

        var utc = value.Value.Kind switch
        {
            DateTimeKind.Utc => value.Value,
            DateTimeKind.Local => value.Value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value.Value, DateTimeKind.Utc),
        };

        return new DateTimeOffset(utc);
    }

    private static ListingPropertyType MapPropertyType(PropertyType type) => type switch
    {
        PropertyType.Apartment  => ListingPropertyType.Apartment,
        PropertyType.House      => ListingPropertyType.House,
        PropertyType.Villa      => ListingPropertyType.Villa,
        PropertyType.Townhouse  => ListingPropertyType.Townhouse,
        PropertyType.Land       => ListingPropertyType.Land,
        PropertyType.Commercial => ListingPropertyType.Commercial,
        PropertyType.Other      => ListingPropertyType.Other,
        _                       => ListingPropertyType.Other,
    };

    private static ListingPriceType MapPriceType(TransactionType transaction) => transaction switch
    {
        TransactionType.Sale => ListingPriceType.Sale,
        TransactionType.Rent => ListingPriceType.Rent,
        _                    => ListingPriceType.Unknown,
    };

    private static ListingStatus MapStatus(PropertyStatus status) => status switch
    {
        PropertyStatus.Active  => ListingStatus.Active,
        PropertyStatus.Pending => ListingStatus.Pending,
        PropertyStatus.Sold    => ListingStatus.Sold,
        PropertyStatus.Rented  => ListingStatus.Rented,
        PropertyStatus.Removed => ListingStatus.Removed,
        _                      => ListingStatus.Active,
    };

    /// <summary>
    /// Resolve an agency name (from IPropertyScraper.AgencyName) to a DB slug.
    /// Returns null if the name is not mapped.
    /// </summary>
    public static string? ResolveAgencySlug(string agencyName)
    {
        return AgencySlugMap.TryGetValue(agencyName, out var slug) ? slug : null;
    }
}

public readonly record struct ListingUpsertResult(ListingUpsertAction Action, Guid ListingId);

public sealed record FieldChange(string? Before, string? After);

public enum ListingUpsertAction
{
    Created,
    Updated,
    Skipped,
}

public readonly record struct BatchUpsertResult(int Created, int Updated, int Skipped);
