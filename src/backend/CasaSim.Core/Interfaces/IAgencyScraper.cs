using CasaSim.Core.Models;

namespace CasaSim.Core.Interfaces;

/// <summary>
/// Rich scraper interface for an individual real-estate agency source.
/// Supports multi-stage scraping (search → detail), structured result
/// reporting with error tracking, and per-source configuration.
/// </summary>
public interface IAgencyScraper
{
    /// <summary>Human-readable agency name (e.g., "Remax", "Century21").</summary>
    string AgencyName { get; }

    /// <summary>Unique URL-safe slug for the agency (e.g., "remax-pombal").</summary>
    string AgencySlug { get; }

    /// <summary>Configuration for this scraper source.</summary>
    ScraperSourceConfig Config { get; }

    /// <summary>
    /// Run a full scrape cycle: search for listings, then fetch details for each.
    /// Returns a <see cref="ScrapeResult"/> with all parsed listings and any errors.
    /// </summary>
    Task<ScrapeResult> ScrapeAllAsync(CancellationToken ct = default);

    /// <summary>
    /// Run the search phase only — returns search results as <see cref="ParsedListing"/>
    /// instances without fetching individual detail pages. Useful for incremental updates
    /// where only new/changed listings need detailed parsing.
    /// </summary>
    Task<ScrapeResult> ScrapeSearchAsync(string? searchParams = null, CancellationToken ct = default);

    /// <summary>
    /// Fetch and parse a single listing detail by its external ID.
    /// Returns null if the listing is not found or cannot be parsed.
    /// </summary>
    Task<ParsedListing?> ScrapeDetailAsync(string externalId, string? sourceUrl = null, CancellationToken ct = default);
}
