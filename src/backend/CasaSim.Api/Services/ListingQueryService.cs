using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Services;

public sealed class ListingQueryService : IListingQueryService
{
    private static readonly HashSet<string> ValidSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "Price", "AreaM2", "Bedrooms", "PublishedAt", "UpdatedAt", "CreatedAt",
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
            .Include(l => l.Images.Where(i => i.IsPrimary));

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
        var items = await query
            .Select(l => new ListingSummaryDto
            {
                Id = l.Id,
                Title = l.Title,
                Price = l.Price,
                PriceType = l.PriceType.ToString(),
                Currency = l.Currency,
                PropertyType = l.PropertyType.ToString(),
                Status = l.Status.ToString(),
                City = l.City,
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
                    .Select(i => new ListingImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        ThumbnailUrl = i.ThumbnailUrl,
                        AltText = i.AltText,
                        IsPrimary = i.IsPrimary,
                        SortOrder = i.SortOrder,
                    })
                    .FirstOrDefault(),
                PublishedAt = l.PublishedAt,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .ToListAsync(ct);

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
            PropertyType = string.IsNullOrWhiteSpace(request.PropertyType) ? null : request.PropertyType,
            PriceType = string.IsNullOrWhiteSpace(request.PriceType) ? null : request.PriceType,
            Status = string.IsNullOrWhiteSpace(request.Status) ? null : request.Status,
            MinPrice = request.MinPrice,
            MaxPrice = request.MaxPrice,
            MinBedrooms = request.MinBedrooms,
            MinAreaM2 = request.MinAreaM2,
            AgencySlug = string.IsNullOrWhiteSpace(request.AgencySlug) ? null : request.AgencySlug,
            SortBy = sortBy,
            SortDirection = sortDir,
            Page = page,
            PageSize = pageSize,
        };
    }

    private static IQueryable<Listing> ApplyFilters(IQueryable<Listing> query, ValidatedSearchRequest v)
    {
        // Default: only active listings
        query = query.Where(l => l.Status == ListingStatus.Active);

        if (v.City is not null)
            query = query.Where(l => l.City != null && EF.Functions.ILike(l.City, $"%{v.City}%"));

        if (v.PropertyType is not null && Enum.TryParse<ListingPropertyType>(v.PropertyType, ignoreCase: true, out var pt))
            query = query.Where(l => l.PropertyType == pt);

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

        if (v.AgencySlug is not null)
            query = query.Where(l => l.Agency != null && l.Agency.Slug == v.AgencySlug);

        return query;
    }

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
            "publishedat" => ascending ? query.OrderBy(l => l.PublishedAt) : query.OrderByDescending(l => l.PublishedAt),
            "updatedat"   => ascending ? query.OrderBy(l => l.UpdatedAt)   : query.OrderByDescending(l => l.UpdatedAt),
            "createdat"   => ascending ? query.OrderBy(l => l.CreatedAt)   : query.OrderByDescending(l => l.CreatedAt),
            _             => query.OrderByDescending(l => l.UpdatedAt),
        };

        return query;
    }
}
