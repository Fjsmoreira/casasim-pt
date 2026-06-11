namespace CasaSim.Core.Models;

/// <summary>
/// Represents a single error encountered during a scrape run.
/// Associated with a specific listing (by ExternalId) or request (by SourceUrl),
/// or a general run-level failure when both are absent.
/// </summary>
public sealed class ScrapeError
{
    /// <summary>Agency being scraped when the error occurred.</summary>
    public string AgencyName { get; init; } = string.Empty;

    /// <summary>
    /// External ID of the listing being processed when the error occurred,
    /// or null if this is a search-phase or run-level error.
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>URL of the request that failed.</summary>
    public string? SourceUrl { get; init; }

    /// <summary>Human-readable error description.</summary>
    public string Message { get; init; } = string.Empty;

    /// <summary>CLR type name of the exception (e.g., "HttpRequestException").</summary>
    public string? ExceptionType { get; init; }

    /// <summary>Exception stack trace, if available.</summary>
    public string? StackTrace { get; init; }

    /// <summary>UTC timestamp when the error was captured.</summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>Severity classification.</summary>
    public ScrapeErrorSeverity Severity { get; init; } = ScrapeErrorSeverity.Warning;
}

/// <summary>
/// Severity of a <see cref="ScrapeError"/>.
/// </summary>
public enum ScrapeErrorSeverity
{
    /// <summary>Non-fatal — a single listing failed to parse, run continues.</summary>
    Warning = 0,

    /// <summary>Partial failure — a batch of listings or a page failed.</summary>
    Error = 1,

    /// <summary>Run-level failure — the entire scrape cycle failed.</summary>
    Critical = 2,
}
