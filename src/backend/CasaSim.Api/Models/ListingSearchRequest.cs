using CasaSim.Core.Data.Entities;

namespace CasaSim.Api.Models;

/// <summary>
/// Search/filter parameters for listing queries.
/// </summary>
public sealed class ListingSearchRequest
{
    // ── Filters ──────────────────────────────────────────────
    public string? City { get; init; }
    public string? PropertyType { get; init; }
    public string? Type { get; init; }
    public string? PriceType { get; init; }
    public string? Transaction { get; init; }
    public string? Status { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBedrooms { get; init; }
    public decimal? MinAreaM2 { get; init; }
    public string? Locality { get; init; }
    public string? AgencySlug { get; init; }
    public string? DealLabel { get; init; }

    // ── Sorting ──────────────────────────────────────────────
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; }

    // ── Pagination ───────────────────────────────────────────
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// Validated version of <see cref="ListingSearchRequest"/> with safe defaults.
/// </summary>
internal sealed record ValidatedSearchRequest
{
    public string? City { get; init; }
    public IReadOnlyList<ListingPropertyType> PropertyTypes { get; init; } = [];
    public string? PriceType { get; init; }
    public string? Status { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBedrooms { get; init; }
    public decimal? MinAreaM2 { get; init; }
    public string? Locality { get; init; }
    public string? AgencySlug { get; init; }
    public string? DealLabel { get; init; }

    public string SortBy { get; init; } = "UpdatedAt";
    public string SortDirection { get; init; } = "Desc";

    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}
