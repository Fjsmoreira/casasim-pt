namespace CasaSim.Api.Models;

public sealed class ListingImageDto
{
    public Guid Id { get; init; }
    public string Url { get; init; } = string.Empty;
    public string? ThumbnailUrl { get; init; }
    public string? AltText { get; init; }
    public bool IsPrimary { get; init; }
    public int SortOrder { get; init; }
}
