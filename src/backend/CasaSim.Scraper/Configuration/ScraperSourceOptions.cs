namespace CasaSim.Scraper.Configuration;

/// <summary>
/// Per-source scraper configuration.
/// Bound from the <c>ScraperSources:{SourceName}</c> config section.
/// </summary>
public sealed class ScraperSourceOptions
{
    /// <summary>
    /// How long to wait between scrape cycles for this source.
    /// Defaults to 6 hours.
    /// </summary>
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);

    /// <summary>
    /// Whether this source should run at all.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
