namespace CasaSim.Core.Models;

/// <summary>
/// Configuration for a single real-estate agency scraper source.
/// Controls HTTP behaviour, endpoint routing, search defaults,
/// and rate-limiting for that specific agency.
/// </summary>
public sealed class ScraperSourceConfig
{
    /// <summary>Human-readable agency name (e.g., "Remax", "Century21").</summary>
    public string AgencyName { get; init; } = string.Empty;

    /// <summary>URL-safe unique slug for DB lookups (e.g., "remax-pombal").</summary>
    public string AgencySlug { get; init; } = string.Empty;

    /// <summary>Base URL for the agency's public website.</summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Optional search endpoint path (relative to <see cref="BaseUrl"/>).
    /// If null, search is performed on the base URL with query parameters.
    /// </summary>
    public string? SearchEndpoint { get; init; }

    /// <summary>
    /// Optional detail-page endpoint pattern.
    /// Supports <c>{externalId}</c> as a placeholder for the listing ID.
    /// Example: <c>"/en/detail/{externalId}"</c>
    /// </summary>
    public string? DetailEndpointPattern { get; init; }

    /// <summary>
    /// Optional API base URL — when the source exposes a JSON API
    /// separate from the public website.
    /// </summary>
    public string? ApiBaseUrl { get; init; }

    /// <summary>Default query-string parameters appended to every search request.</summary>
    public Dictionary<string, string> DefaultSearchParams { get; init; } = new()
    {
        ["city"] = "Pombal",
    };

    /// <summary>Minimum delay (in milliseconds) between consecutive requests.</summary>
    public int RequestDelayMs { get; init; } = 1000;

    /// <summary>Maximum retry attempts for transient failures.</summary>
    public int MaxRetries { get; init; } = 3;

    /// <summary>Number of listings per search-results page.</summary>
    public int PageSize { get; init; } = 48;

    /// <summary>Whether this source is currently enabled for scraping.</summary>
    public bool Enabled { get; init; } = true;
}
