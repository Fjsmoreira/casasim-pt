namespace CasaSim.Api.Models;

/// <summary>
/// Lightweight listing row for the admin management table.
/// </summary>
public sealed class AdminListingDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal? Price { get; init; }
    public string? PriceFormatted { get; init; }
    public string Currency { get; init; } = "EUR";
    public string PropertyType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? City { get; init; }
    public int? Bedrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public string? ThumbnailUrl { get; init; }
    public string? AgencyName { get; init; }
    public string? AgencySlug { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
