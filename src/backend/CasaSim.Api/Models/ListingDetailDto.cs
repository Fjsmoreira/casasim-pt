using CasaSim.Core.Data.Entities;

namespace CasaSim.Api.Models;

/// <summary>
/// Full listing detail returned by GET /api/listings/{id}.
/// Contains all fields including description, all images, features, and agency info.
/// </summary>
public sealed class ListingDetailDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal? Price { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string PropertyType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;

    // --- Location ---
    public string? City { get; init; }
    public string? Parish { get; init; }
    public string? District { get; init; }
    public string? Description { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    // --- Details ---
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public decimal? LandAreaM2 { get; init; }

    // --- Media ---
    /// <summary>All image URLs ordered by SortOrder.</summary>
    public List<string> Images { get; init; } = [];
    public ListingImageDto? PrimaryImage { get; init; }

    // --- Features ---
    public List<string> Features { get; init; } = [];

    // --- Source / External ---
    public string? SourceUrl { get; init; }
    public string? ExternalId { get; init; }

    // --- Agency ---
    public string? AgencyName { get; init; }
    public string? AgencyPhone { get; init; }
    public string? AgencyEmail { get; init; }
    public string? AgencyWebsiteUrl { get; init; }

    // --- Timestamps ---
    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
