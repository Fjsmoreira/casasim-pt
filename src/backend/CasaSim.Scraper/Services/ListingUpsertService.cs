using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Core.Models;
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
    };

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
        CancellationToken ct = default)
    {
        using var agencySlugScope = LogContext.PushProperty("agencySlug", agencySlug);
        using var sourceIdScope = LogContext.PushProperty("sourceId", property.ExternalId);

        // ── Resolve agency ────────────────────────────────────
        var agency = await _db.Agencies
            .AsTracking()
            .FirstOrDefaultAsync(a => a.Slug == agencySlug, ct);

        if (agency is null)
        {
            _logger.LogWarning("Agency slug '{Slug}' not found in DB; skipping listing '{Id}'",
                agencySlug, property.ExternalId);
            return new ListingUpsertResult(ListingUpsertAction.Skipped, Guid.Empty);
        }

        // ── Look up existing listing by (AgencyId, ExternalId) ──
        var existing = await _db.Listings
            .AsTracking()
            .Include(l => l.Images)
            .FirstOrDefaultAsync(
                l => l.AgencyId == agency.Id && l.ExternalId == property.ExternalId,
                ct);

        if (existing is not null)
        {
            // ── Update ────────────────────────────────────────
            MapPropertyToListing(property, existing, agency.Id);
            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.LastSeenAt = DateTimeOffset.UtcNow;

            // Replace images: remove old, add fresh set (works with PostgreSQL cascade delete)
            ReplaceImages(existing, property.Images);

            using var listingScope = LogContext.PushProperty("listingId", existing.Id);
            _logger.LogDebug("Updated listing {SourceId} ({Title})",
                property.ExternalId, property.Title);

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
        CancellationToken ct = default)
    {
        using var agencySlugScope = LogContext.PushProperty("agencySlug", agencySlug);

        var created = 0;
        var updated = 0;
        var skipped = 0;

        foreach (var property in properties)
        {
            var result = await UpsertAsync(property, agencySlug, ct);
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

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Batch upsert for {Slug}: {Created} created, {Updated} updated, {Skipped} skipped",
            agencySlug, created, updated, skipped);

        return new BatchUpsertResult(created, updated, skipped);
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

        if (property.Location is not null)
        {
            // We store the point directly — LocationId FK can be null
            // for listings without a dedicated Location entity.
        }

        return listing;
    }

    private void ReplaceImages(Listing listing, List<string> imageUrls)
    {
        // Remove old images (EF cascade delete with PostgreSQL handles cleanup)
        listing.Images.Clear();

        for (var i = 0; i < imageUrls.Count; i++)
        {
            listing.Images.Add(new ListingImage
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
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

public enum ListingUpsertAction
{
    Created,
    Updated,
    Skipped,
}

public readonly record struct BatchUpsertResult(int Created, int Updated, int Skipped);
