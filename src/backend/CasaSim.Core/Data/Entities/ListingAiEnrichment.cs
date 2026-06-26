namespace CasaSim.Core.Data.Entities;

public sealed class ListingAiEnrichment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid ListingId { get; set; }
    public Listing? Listing { get; set; }

    public string SourceHash { get; set; } = string.Empty;
    public string? GeneratedDescription { get; set; }
    public string? CorrectedFactsJson { get; set; }
    public string? CorrectionAuditJson { get; set; }
    public string? ExtractedFactsJson { get; set; }
    public string? HighlightsJson { get; set; }
    public decimal? DealScore { get; set; }
    public string? DealLabel { get; set; }
    public string? DealReasonsJson { get; set; }
    public string? WarningsJson { get; set; }
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public ListingAiEnrichmentStatus Status { get; set; } = ListingAiEnrichmentStatus.Pending;
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
    public DateTimeOffset? LastAnalyzedAt { get; set; }
    public DateTimeOffset? NextRetryAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ListingAiEnrichmentStatus
{
    Pending = 0,
    Succeeded,
    Failed,
}
