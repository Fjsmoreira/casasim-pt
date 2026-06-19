namespace CasaSim.Core.Data.Entities;

public sealed class ScrapeListingChange
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ScrapeLogId { get; set; }
    public ScrapeLog? ScrapeLog { get; set; }

    public Guid? ListingId { get; set; }
    public Listing? Listing { get; set; }

    public ScrapeListingChangeAction Action { get; set; }
    public string AgencySlug { get; set; } = string.Empty;
    public string ExternalId { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? SourceUrl { get; set; }
    public string? ChangeSummaryJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ScrapeListingChangeAction
{
    Created = 0,
    Updated,
    Removed,
    Skipped
}
