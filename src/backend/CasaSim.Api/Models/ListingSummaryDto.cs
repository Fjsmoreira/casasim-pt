namespace CasaSim.Api.Models;

public sealed class ListingSummaryDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public decimal? Price { get; init; }
    public string PriceType { get; init; } = string.Empty;
    public string Currency { get; init; } = string.Empty;
    public string PropertyType { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? City { get; init; }
    public string? Parish { get; init; }
    public int? Bedrooms { get; init; }
    public int? Bathrooms { get; init; }
    public decimal? AreaM2 { get; init; }
    public decimal? LandAreaM2 { get; init; }

    public AgencyDto? Agency { get; init; }
    public ListingImageDto? PrimaryImage { get; init; }
    public List<ListingImageDto> Images { get; init; } = [];

    public DateTimeOffset? PublishedAt { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
}
