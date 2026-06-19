using CasaSim.Core.Interfaces;
using CasaSim.Core.Data.Entities;
using CasaSim.Api;
using CasaSim.Scraper.Configuration;
using CasaSim.Scraper.Diagnostics;
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog.Context;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Background runner that schedules scraper executions using <see cref="PeriodicTimer"/>
/// with configurable per-source intervals and enabled flags.
///
/// Runs scrapers sequentially within each tick.  Exceptions from individual scrapers
/// are captured and written to ScrapeLogs via <see cref="ScrapeLoggingService"/>.
/// </summary>
internal sealed class ScraperOrchestrator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperOrchestrator> _logger;
    private readonly IOptionsMonitor<Dictionary<string, ScraperSourceOptions>> _sourceOptions;

    public ScraperOrchestrator(
        IServiceScopeFactory scopeFactory,
        ILogger<ScraperOrchestrator> logger,
        IOptionsMonitor<Dictionary<string, ScraperSourceOptions>> sourceOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _sourceOptions = sourceOptions;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraper orchestrator started (PeriodicTimer mode)");

        var tickInterval = TimeSpan.FromMinutes(1);
        _logger.LogInformation("Tick interval: {Interval}", tickInterval);

        // ── Last-run tracking ──────────────────────────────────
        var lastRun = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);

        using var timer = new PeriodicTimer(tickInterval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            var now = DateTime.UtcNow;

            var due = await DiscoverDueSourcesAsync(lastRun, now, stoppingToken);

            if (due.Count == 0)
                continue;

            _logger.LogDebug("Tick: {Count} source(s) due", due.Count);

            foreach (var source in due)
            {
                if (stoppingToken.IsCancellationRequested)
                    break;

                // Each scraper gets its own DI scope so it owns its own
                // AppDbContext.  Without this, all scrapers in the same
                // tick share one DbContext, and the change-tracker state
                // from the first scraper's SaveChangesAsync corrupts the
                // second scraper's operations (DbUpdateConcurrencyException).
                using var scope = _scopeFactory.CreateScope();
                var services = scope.ServiceProvider;

                var scraper = ResolveScraper(source.ScraperKey, services);
                if (scraper is null)
                    continue;

                await RunSingleScraperAsync(scraper, source, services, stoppingToken);
                lastRun[source.ScraperKey] = DateTime.UtcNow;

                if (source.ManualRunRequestedAt is not null)
                    await ClearManualRunRequestAsync(source.Id, source.ManualRunRequestedAt.Value, stoppingToken);
            }
        }
    }

    // ── Source discovery ────────────────────────────────────────

    /// <summary>
    /// Returns <c>agencyName → (agencyName, ScraperSourceOptions)</c> for every
    /// enabled source that has a config section and is marked Enabled = true.
    /// </summary>
    private async Task<List<ScraperSource>> DiscoverDueSourcesAsync(
        Dictionary<string, DateTime> lastRun,
        DateTime now,
        CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
        {
            return DiscoverDueSourcesFromConfiguration(lastRun, now);
        }

        var sources = await db.ScraperSources
            .AsNoTracking()
            .Where(s => s.Enabled)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        return sources
            .Where(s => s.ManualRunRequestedAt is not null || IsDue(s.ScraperKey, s.Interval, lastRun, now))
            .ToList();
    }

    private List<ScraperSource> DiscoverDueSourcesFromConfiguration(
        Dictionary<string, DateTime> lastRun,
        DateTime now)
    {
        var allConfig = _sourceOptions.CurrentValue;
        if (allConfig is null)
            return [];

        return allConfig
            .Where(kvp => kvp.Value.Enabled)
            .Where(kvp => IsDue(kvp.Key, kvp.Value.Interval, lastRun, now))
            .Select(kvp => new ScraperSource
            {
                Name = kvp.Key,
                ScraperKey = kvp.Key,
                AgencySlug = ListingUpsertService.ResolveAgencySlug(kvp.Key) ?? string.Empty,
                Interval = kvp.Value.Interval,
                Enabled = kvp.Value.Enabled,
            })
            .ToList();
    }

    /// <summary>
    /// A source is due when its configured interval has elapsed since its last run,
    /// or it has never run (first-tick behaviour).
    /// </summary>
    private static bool IsDue(
        string name,
        TimeSpan interval,
        Dictionary<string, DateTime> lastRun,
        DateTime now)
    {
        if (!lastRun.TryGetValue(name, out var last))
            return true; // never run → due

        return now - last >= interval;
    }

    private async Task ClearManualRunRequestAsync(Guid sourceId, DateTimeOffset requestedAt, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetService<AppDbContext>();
        if (db is null)
            return;

        await db.ScraperSources
            .Where(s => s.Id == sourceId && s.ManualRunRequestedAt == requestedAt)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.ManualRunRequestedAt, (DateTimeOffset?)null)
                .SetProperty(s => s.UpdatedAt, DateTimeOffset.UtcNow), ct);
    }

    /// <summary>
    /// Try to resolve <see cref="IPropertyScraper"/> by matching <see cref="IPropertyScraper.AgencyName"/>.
    /// Abstracted so we don't cache scraper instances beyond their DI lifetime.
    /// </summary>
    private static IPropertyScraper? ResolveScraper(string agencyName, IServiceProvider services)
    {
        var all = services.GetServices<IPropertyScraper>().ToList();

        if (all.Count == 0)
        {
            // Fallback: some scrapers may only be registered as concrete types.
            // This path resolves from the type name.
            return null;
        }

        var match = all.FirstOrDefault(s =>
            string.Equals(s.AgencyName, agencyName, StringComparison.OrdinalIgnoreCase));

        if (match is null)
        {
            // Try case-insensitive partial match as a last resort.
            match = all.FirstOrDefault(s =>
                s.AgencyName.Contains(agencyName, StringComparison.OrdinalIgnoreCase));
        }

        return match;
    }

    // ── Scraper execution ───────────────────────────────────────

    /// <summary>
    /// Runs a single scraper through its full lifecycle, logging via ScrapeLoggingService
    /// and emitting OpenTelemetry spans and metrics.
    /// </summary>
    private async Task RunSingleScraperAsync(
        IPropertyScraper scraper,
        ScraperSource source,
        IServiceProvider services,
        CancellationToken ct)
    {
        var agencyName = scraper.AgencyName;
        var agencySlug = !string.IsNullOrWhiteSpace(source.AgencySlug)
            ? source.AgencySlug
            : ListingUpsertService.ResolveAgencySlug(agencyName);

        using var scraperScope = LogContext.PushProperty("scraperName", scraper.GetType().Name);

        if (agencySlug is null)
        {
            using var agencyNameScope = LogContext.PushProperty("agencyName", agencyName);
            _logger.LogWarning("No agency slug mapping for '{Name}'; skipping", agencyName);
            return;
        }

        using var agencySlugScope = LogContext.PushProperty("agencySlug", agencySlug);
        using var agencyScope = LogContext.PushProperty("agencyName", agencyName);

        _logger.LogInformation("Scraping {Agency}", agencyName);

        // ── OpenTelemetry span ─────────────────────────────────
        using var activity = ScraperDiagnostics.ActivitySource.StartActivity(
            ScraperDiagnostics.SpanScraperRun,
            ActivityKind.Internal);

        activity?.SetTag(ScraperDiagnostics.TagScraper, agencyName);
        activity?.SetTag(ScraperDiagnostics.TagAgencySlug, agencySlug);

        var stopwatch = Stopwatch.StartNew();
        var status = ScraperDiagnostics.StatusSuccess;

        var logging = services.GetRequiredService<ScrapeLoggingService>();
        var agencyId = await logging.ResolveAgencyIdAsync(agencySlug, ct);
        var logId = await logging.StartLogAsync(agencyName, source.SourceUrl, agencyId, ct: ct);

        IReadOnlyList<Core.Models.Property> properties;
        try
        {
            properties = await scraper.ScrapeAsync(ct);

            _logger.LogInformation("Scraped {Count} properties from {Agency}", properties.Count, agencyName);

            // Record discovered count metric
            ScraperDiagnostics.ListingsDiscoveredTotal.Add(properties.Count,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug));

            if (properties.Count == 0)
            {
                await logging.CompleteLogAsync(logId,
                    listingsFound: 0, listingsCreated: 0,
                    listingsUpdated: 0, listingsRemoved: 0, ct: ct);
                return;
            }

            var upsertService = services.GetRequiredService<ListingUpsertService>();
            var result = await upsertService.UpsertBatchAsync(properties, agencySlug, logId, ct);
            var seenExternalIds = properties
                .Select(p => p.ExternalId)
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var removed = await upsertService.MarkMissingListingsRemovedAsync(agencySlug, seenExternalIds, logId, ct);

            _logger.LogInformation(
                "{Agency}: {Created} created, {Updated} updated, {Skipped} skipped, {Removed} removed",
                agencyName, result.Created, result.Updated, result.Skipped, removed);

            // Record upserted count metric
            ScraperDiagnostics.ListingsUpsertedTotal.Add(result.Created,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug),
                new(ScraperDiagnostics.TagAction, "created"));
            ScraperDiagnostics.ListingsUpsertedTotal.Add(result.Updated,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug),
                new(ScraperDiagnostics.TagAction, "updated"));
            ScraperDiagnostics.ListingsUpsertedTotal.Add(result.Skipped,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug),
                new(ScraperDiagnostics.TagAction, "skipped"));

            await logging.CompleteLogAsync(logId,
                listingsFound: properties.Count,
                listingsCreated: result.Created,
                listingsUpdated: result.Updated,
                listingsRemoved: removed,
                ct: ct);
        }
        catch (Exception ex)
        {
            status = ScraperDiagnostics.StatusFail;
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            activity?.SetTag(ScraperDiagnostics.TagErrorDetail, ex.ToString());

            _logger.LogError(ex, "Failed to scrape/upsert {Agency}", agencyName);

            try
            {
                // Reset EF Core change tracker so FailLogAsync doesn't inherit
                // a corrupted state from the failed upsert.
                if (services.GetRequiredService<CasaSim.Api.AppDbContext>() is { } db)
                {
                    db.ChangeTracker.Clear();
                }
            }
            catch
            {
                // Best-effort — if Clear() fails, FailLogAsync may also fail
                // but that's handled next.
            }

            try
            {
                await logging.FailLogAsync(logId,
                    errorMessage: ex.Message,
                    errorDetails: ex.ToString(),
                    ct: ct);
            }
            catch (Exception failEx)
            {
                _logger.LogError(failEx, "Failed to write error log for {Agency}", agencyName);
            }
        }
        finally
        {
            stopwatch.Stop();
            var elapsedSec = stopwatch.Elapsed.TotalSeconds;

            // Record run count metric
            ScraperDiagnostics.ScrapeRunsTotal.Add(1,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug),
                new(ScraperDiagnostics.TagStatus, status));

            // Record duration histogram
            ScraperDiagnostics.ScrapeDurationSeconds.Record(elapsedSec,
                new(ScraperDiagnostics.TagScraper, agencyName),
                new(ScraperDiagnostics.TagAgencySlug, agencySlug),
                new(ScraperDiagnostics.TagStatus, status));

            // Record error count if failed
            if (status == ScraperDiagnostics.StatusFail)
            {
                ScraperDiagnostics.ScrapeErrorsTotal.Add(1,
                    new(ScraperDiagnostics.TagScraper, agencyName),
                    new(ScraperDiagnostics.TagAgencySlug, agencySlug));
            }

            // Finalize the span
            activity?.SetTag(ScraperDiagnostics.TagStatus, status);
            activity?.SetTag("duration_ms", stopwatch.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Blocks forever (or until cancellation) when no sources are enabled,
    /// so the hosted service stays alive and doesn't spin.
    /// </summary>
    private static async Task WaitIndefinitely(CancellationToken ct)
    {
        var tcs = new TaskCompletionSource();
        await using var _ = ct.Register(() => tcs.TrySetResult());
        await tcs.Task;
    }
}
