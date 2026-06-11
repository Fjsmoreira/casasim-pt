namespace CasaSim.Core.Models;

/// <summary>
/// Structured result for a single scrape run against one agency source.
/// Aggregates successfully parsed listings, per-listing errors, and
/// run-level metadata (timing, totals).
/// </summary>
public sealed class ScrapeResult
{
    /// <summary>Agency name that was scraped.</summary>
    public string AgencyName { get; init; } = string.Empty;

    /// <summary>URL-safe agency slug.</summary>
    public string AgencySlug { get; init; } = string.Empty;

    /// <summary>UTC timestamp when the scrape started.</summary>
    public DateTime StartedAt { get; init; } = DateTime.UtcNow;

    /// <summary>UTC timestamp when the scrape completed (or failed).</summary>
    public DateTime? CompletedAt { get; init; }

    /// <summary>Wall-clock duration of the scrape run.</summary>
    public TimeSpan? Duration =>
        CompletedAt.HasValue ? CompletedAt.Value - StartedAt : null;

    /// <summary>Successfully parsed listings.</summary>
    public IReadOnlyList<ParsedListing> Listings { get; init; } = Array.Empty<ParsedListing>();

    /// <summary>Errors encountered per-listing or per-request.</summary>
    public IReadOnlyList<ScrapeError> Errors { get; init; } = Array.Empty<ScrapeError>();

    /// <summary>Total listings the search endpoint reported (before detail parsing).</summary>
    public int TotalFound { get; init; }

    /// <summary>Number of listings successfully parsed.</summary>
    public int SuccessCount => Listings.Count;

    /// <summary>Number of errors recorded.</summary>
    public int ErrorCount => Errors.Count;

    /// <summary>Whether the scrape completed without critical errors.</summary>
    public bool IsSuccess => ErrorCount == 0;

    /// <summary>Whether any listings were found at all.</summary>
    public bool HasListings => Listings.Count > 0;
}
