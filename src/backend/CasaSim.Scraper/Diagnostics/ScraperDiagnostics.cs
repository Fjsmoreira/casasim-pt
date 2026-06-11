using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace CasaSim.Scraper.Diagnostics;

/// <summary>
/// Central diagnostic definitions for the scraper — ActivitySource for tracing
/// and Meter for metrics.  Consumers reference the static instances so that
/// OpenTelemetry's <c>AddSource</c> / <c>AddMeter</c> can pick them up at
/// startup without compile-time coupling.
/// </summary>
public static class ScraperDiagnostics
{
    /// <summary>
    /// Activity source name — used by OpenTelemetry tracing.
    /// Span names follow the pattern <c>scraper.&lt;phase&gt;</c>.
    /// Tags: <c>scraper</c> (agency name), <c>agency_slug</c>, <c>status</c>.
    /// </summary>
    public static readonly ActivitySource ActivitySource = new("CasaSim.Scraper");

    /// <summary>
    /// Meter name — used by OpenTelemetry metrics.
    /// </summary>
    public static readonly Meter Meter = new("CasaSim.Scraper");

    // ── Counters ─────────────────────────────────────────────────────

    /// <summary>Total scrape runs, tagged by scraper, agency_slug, status.</summary>
    public static readonly Counter<long> ScrapeRunsTotal = Meter.CreateCounter<long>(
        "scrape_runs_total",
        description: "Total number of scrape runs initiated");

    /// <summary>Listings discovered per scrape run, tagged by scraper, agency_slug.</summary>
    public static readonly Counter<long> ListingsDiscoveredTotal = Meter.CreateCounter<long>(
        "listings_discovered_total",
        description: "Total number of listings discovered by scrape runs");

    /// <summary>Listings upserted (created / updated / skipped), tagged by scraper, agency_slug, action.</summary>
    public static readonly Counter<long> ListingsUpsertedTotal = Meter.CreateCounter<long>(
        "listings_upserted_total",
        description: "Total number of listings upserted (created/updated/skipped)");

    /// <summary>Scrape errors, tagged by scraper, agency_slug.</summary>
    public static readonly Counter<long> ScrapeErrorsTotal = Meter.CreateCounter<long>(
        "scrape_errors_total",
        description: "Total number of scrape errors");

    // ── Histograms ───────────────────────────────────────────────────

    /// <summary>Scrape run duration in seconds, tagged by scraper, agency_slug, status.</summary>
    public static readonly Histogram<double> ScrapeDurationSeconds = Meter.CreateHistogram<double>(
        "scrape_duration_seconds",
        unit: "s",
        description: "Duration of scrape runs");

    // ── Span names ───────────────────────────────────────────────────

    /// <summary>Span name for a single agency scrape (discover + parse + upsert).</summary>
    public const string SpanScraperRun = "scraper.run";

    /// <summary>Span name for the upsert phase within a scrape run.</summary>
    public const string SpanUpsertBatch = "scraper.upsert_batch";

    // ── Tag keys ─────────────────────────────────────────────────────

    public const string TagScraper     = "scraper";
    public const string TagAgencySlug  = "agency_slug";
    public const string TagStatus      = "status";
    public const string TagAction      = "action";
    public const string TagErrorDetail = "error.detail";

    // ── Tag values ───────────────────────────────────────────────────

    public const string StatusSuccess = "success";
    public const string StatusFail    = "fail";
}
