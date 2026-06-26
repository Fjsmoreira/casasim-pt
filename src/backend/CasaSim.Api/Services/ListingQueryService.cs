using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Services;

public sealed class ListingQueryService : IListingQueryService
{
    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Price", "AreaM2", "Bedrooms", "PublishedAt", "FirstSeenAt", "LastSeenAt", "UpdatedAt", "CreatedAt",
    };

    private static readonly HashSet<string> ValidSortDirections = new(StringComparer.OrdinalIgnoreCase)
    {
        "Asc", "Desc",
    };

    private readonly AppDbContext _db;

    public ListingQueryService(AppDbContext db) => _db = db;

    public async Task<PagedResult<ListingSummaryDto>> SearchAsync(
        ListingSearchRequest request, CancellationToken ct = default)
    {
        var validated = Validate(request);

        // ── Base query ───────────────────────────────────────
        IQueryable<Listing> query = _db.Listings
            .AsNoTracking()
            .Include(l => l.Agency)
            .Include(l => l.Location)
            .Include(l => l.Images);

        // ── Filters ──────────────────────────────────────────
        query = ApplyFilters(query, validated);

        // ── Total count (before pagination) ──────────────────
        var totalCount = await query.CountAsync(ct);

        // ── Sorting ──────────────────────────────────────────
        query = ApplySorting(query, validated);

        // ── Pagination ───────────────────────────────────────
        query = query
            .Skip((validated.Page - 1) * validated.PageSize)
            .Take(validated.PageSize);

        // ── Project + execute ────────────────────────────────
        var listings = await query.ToListAsync(ct);
        var listingIds = listings.Select(l => l.Id).ToArray();
        var aiByListingId = await _db.ListingAiEnrichments
            .AsNoTracking()
            .Where(e => listingIds.Contains(e.ListingId))
            .ToDictionaryAsync(e => e.ListingId, ct);

        var items = listings
            .Select(l =>
            {
                aiByListingId.TryGetValue(l.Id, out var enrichment);
                return new ListingSummaryDto
                {
                    Id = l.Id,
                    Title = l.Title,
                    Price = l.Price,
                    PriceType = l.PriceType.ToString(),
                    Currency = l.Currency,
                    PropertyType = l.PropertyType.ToString(),
                    Status = l.Status.ToString(),
                    City = l.City,
                    Parish = l.Location?.Parish,
                    Bedrooms = l.Bedrooms,
                    Bathrooms = l.Bathrooms,
                    AreaM2 = l.AreaM2,
                    LandAreaM2 = l.LandAreaM2,
                    Agency = l.Agency == null
                        ? null
                        : new AgencyDto
                        {
                            Id = l.Agency.Id,
                            Name = l.Agency.Name,
                            Slug = l.Agency.Slug,
                            WebsiteUrl = l.Agency.WebsiteUrl,
                            ContactEmail = l.Agency.ContactEmail,
                            ContactPhone = l.Agency.ContactPhone,
                        },
                    PrimaryImage = l.Images
                        .Where(i => i.IsPrimary)
                        .OrderBy(i => i.SortOrder)
                        .Select(ToImageDto)
                        .FirstOrDefault(),
                    Images = l.Images
                        .OrderBy(i => i.IsPrimary ? 0 : 1)
                        .ThenBy(i => i.SortOrder)
                        .Take(3)
                        .Select(ToImageDto)
                        .ToList(),
                    Ai = ListingAiDtoMapper.FromEnrichment(enrichment),
                    PublishedAt = l.PublishedAt,
                    FirstSeenAt = l.FirstSeenAt,
                    LastSeenAt = l.LastSeenAt,
                    CreatedAt = l.CreatedAt,
                    UpdatedAt = l.UpdatedAt,
                };
            })
            .ToList();

        return new PagedResult<ListingSummaryDto>
        {
            Items = items,
            Page = validated.Page,
            PageSize = validated.PageSize,
            TotalCount = totalCount,
        };
    }

    // ── Internal helpers ────────────────────────────────────

    private static ValidatedSearchRequest Validate(ListingSearchRequest request)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        var sortBy = !string.IsNullOrWhiteSpace(request.SortBy) && ValidSortFields.Contains(request.SortBy)
            ? request.SortBy
            : "UpdatedAt";

        var sortDir = !string.IsNullOrWhiteSpace(request.SortDirection) && ValidSortDirections.Contains(request.SortDirection)
            ? request.SortDirection
            : "Desc";

        return new ValidatedSearchRequest
        {
            City = string.IsNullOrWhiteSpace(request.City) ? null : request.City.Trim(),
            PropertyTypes = ParsePropertyTypes(request.PropertyType ?? request.Type),
            PriceType = string.IsNullOrWhiteSpace(request.PriceType ?? request.Transaction) ? null : (request.PriceType ?? request.Transaction),
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinBedrooms = request.MinBedrooms,
            MinAreaM2 = request.MinAreaM2,
            Locality = string.IsNullOrWhiteSpace(request.Locality) ? null : request.Locality.Trim(),
            AgencySlug = string.IsNullOrWhiteSpace(request.AgencySlug) ? null : request.AgencySlug,
            DealLabel = NormalizeDealLabel(request.DealLabel),
            SortBy = sortBy,
            SortDirection = sortDir,
            Page = page,
            PageSize = pageSize,
        };
    }

    private IQueryable<Listing> ApplyFilters(IQueryable<Listing> query, ValidatedSearchRequest v)
    {
        // Default: only active listings
        query = query.Where(l => l.Status == ListingStatus.Active);

        if (v.City is not null)
            query = query.Where(l => l.City != null && EF.Functions.ILike(l.City, $"%{v.City}%"));

        if (v.PropertyTypes.Count > 0)
            query = query.Where(l => v.PropertyTypes.Contains(l.PropertyType));

        if (v.PriceType is not null && Enum.TryParse<ListingPriceType>(v.PriceType, ignoreCase: true, out var prt))
            query = query.Where(l => l.PriceType == prt);

        if (v.Status is not null && Enum.TryParse<ListingStatus>(v.Status, ignoreCase: true, out var st))
            query = query.Where(l => l.Status == st);

        if (v.MinPrice.HasValue)
            query = query.Where(l => l.Price >= v.MinPrice.Value);

        if (v.MaxPrice.HasValue)
            query = query.Where(l => l.Price <= v.MaxPrice.Value);

        if (v.MinBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= v.MinBedrooms.Value);

        if (v.MinAreaM2.HasValue)
            query = query.Where(l => l.AreaM2 >= v.MinAreaM2.Value);

        if (v.Locality is not null)
        {
            var locality = v.Locality;

            // Current production data has localidade/freguesia embedded in titles,
            // e.g. "Moradia T2 à venda em Abiul, Pombal". Prefer normalized
            // Location.Parish once populated; keep title matching as a fallback
            // for today's scraped data.
            query = query.Where(l =>
                (l.Location != null && l.Location.Parish != null && EF.Functions.ILike(l.Location.Parish, locality)) ||
                (EF.Functions.ILike(l.Title, $"% em {locality}, Pombal%") ||
                 EF.Functions.ILike(l.Title, $"% no {locality}, Pombal%") ||
                 EF.Functions.ILike(l.Title, $"% na {locality}, Pombal%") ||
                 EF.Functions.ILike(l.Title, $"%| {locality} | Pombal%") ||
                 EF.Functions.ILike(l.Title, $"%|| {locality}%")));
        }

        if (v.AgencySlug is not null)
            query = query.Where(l => l.Agency != null && l.Agency.Slug == v.AgencySlug);

        if (v.DealLabel is not null)
            query = query.Where(l => _db.ListingAiEnrichments.Any(e =>
                e.ListingId == l.Id &&
                e.Status == ListingAiEnrichmentStatus.Succeeded &&
                e.DealLabel == v.DealLabel));

        return query;
    }

    private static IReadOnlyList<ListingPropertyType> ParsePropertyTypes(string? propertyTypes) =>
        string.IsNullOrWhiteSpace(propertyTypes)
            ? []
            : propertyTypes
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Select(type => Enum.TryParse<ListingPropertyType>(type, ignoreCase: true, out var value) ? value : (ListingPropertyType?)null)
                .Where(type => type.HasValue)
                .Select(type => type!.Value)
                .Distinct()
                .ToArray();

    private static IQueryable<Listing> ApplySorting(IQueryable<Listing> query, ValidatedSearchRequest v)
    {
        var ascending = string.Equals(v.SortDirection, "Asc", StringComparison.OrdinalIgnoreCase);

        // NOTE: we map the sort field *after* including Agency, so these are EF Core
        // expression-based OrderBy / OrderByDescending calls.
        query = v.SortBy.ToLowerInvariant() switch
        {
            "price"       => ascending ? query.OrderBy(l => l.Price)       : query.OrderByDescending(l => l.Price),
            "aream2"      => ascending ? query.OrderBy(l => l.AreaM2)      : query.OrderByDescending(l => l.AreaM2),
            "bedrooms"    => ascending ? query.OrderBy(l => l.Bedrooms)    : query.OrderByDescending(l => l.Bedrooms),
            "publishedat" => ascending ? query.OrderBy(l => l.PublishedAt ?? l.FirstSeenAt) : query.OrderByDescending(l => l.PublishedAt ?? l.FirstSeenAt),
            "firstseenat" => ascending ? query.OrderBy(l => l.FirstSeenAt) : query.OrderByDescending(l => l.FirstSeenAt),
            "lastseenat"  => ascending ? query.OrderBy(l => l.LastSeenAt)  : query.OrderByDescending(l => l.LastSeenAt),
            "updatedat"   => ascending ? query.OrderBy(l => l.UpdatedAt)   : query.OrderByDescending(l => l.UpdatedAt),
            "createdat"   => ascending ? query.OrderBy(l => l.CreatedAt)   : query.OrderByDescending(l => l.CreatedAt),
            _             => query.OrderByDescending(l => l.UpdatedAt),
        };

        return query;
    }

    private static ListingImageDto ToImageDto(ListingImage image) => new()
    {
        Id = image.Id,
        Url = image.Url,
        ThumbnailUrl = image.ThumbnailUrl,
        AltText = image.AltText,
        IsPrimary = image.IsPrimary,
        SortOrder = image.SortOrder,
    };

    private static string? NormalizeDealLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        return value.Trim().ToLowerInvariant() switch
        {
            "gooddeal" or "good" => "GoodDeal",
            "neutral" => "Neutral",
            "baddeal" or "bad" => "BadDeal",
            _ => null,
        };
    }
}
