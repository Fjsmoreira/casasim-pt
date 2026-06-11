using CasaSim.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

internal sealed class ScraperOrchestrator : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScraperOrchestrator> _logger;

    public ScraperOrchestrator(
        IServiceScopeFactory scopeFactory,
        ILogger<ScraperOrchestrator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraper orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _scopeFactory.CreateScope();
            var services = scope.ServiceProvider;

            try
            {
                await RunScrapeCycleAsync(services, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Scrape cycle failed");
            }

            // Wait 6 hours before next cycle
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }

    private async Task RunScrapeCycleAsync(IServiceProvider services, CancellationToken ct)
    {
        _logger.LogInformation("Starting scrape cycle at {Time}", DateTimeOffset.UtcNow);

        // ── Discover all registered scrapers ──────────────────
        var scrapers = services.GetServices<IPropertyScraper>().ToList();
        if (scrapers.Count == 0)
        {
            _logger.LogInformation("No IPropertyScraper implementations registered");
        }

        foreach (var scraper in scrapers)
        {
            await RunSingleScraperAsync(scraper, services, ct);
        }

        _logger.LogInformation("Scrape cycle complete");
    }

    private async Task RunSingleScraperAsync(
        IPropertyScraper scraper,
        IServiceProvider services,
        CancellationToken ct)
    {
        var agencyName = scraper.AgencyName;
        var agencySlug = ListingUpsertService.ResolveAgencySlug(agencyName);

        if (agencySlug is null)
        {
            _logger.LogWarning("No agency slug mapping for '{Name}'; skipping", agencyName);
            return;
        }

        _logger.LogInformation("Scraping {Agency}", agencyName);

        // ── Scrape logging ───────────────────────────────────
        var logging = services.GetRequiredService<ScrapeLoggingService>();
        var agencyId = await logging.ResolveAgencyIdAsync(agencySlug, ct);
        var logId = await logging.StartLogAsync(
            agencyName,
            sourceUrl: null,
            agencyId: agencyId,
            ct: ct);

        IReadOnlyList<Core.Models.Property> properties;
        try
        {
            // ── Scrape ────────────────────────────────────────
            properties = await scraper.ScrapeAsync(ct);

            _logger.LogInformation("Scraped {Count} properties from {Agency}", properties.Count, agencyName);

            if (properties.Count == 0)
            {
                await logging.CompleteLogAsync(logId,
                    listingsFound: 0,
                    listingsCreated: 0,
                    listingsUpdated: 0,
                    listingsRemoved: 0,
                    ct: ct);
                return;
            }

            // ── Upsert ────────────────────────────────────────
            var upsertService = services.GetRequiredService<ListingUpsertService>();
            var result = await upsertService.UpsertBatchAsync(properties, agencySlug, ct);

            _logger.LogInformation(
                "{Agency}: {Created} created, {Updated} updated, {Skipped} skipped",
                agencyName, result.Created, result.Updated, result.Skipped);

            await logging.CompleteLogAsync(logId,
                listingsFound: properties.Count,
                listingsCreated: result.Created,
                listingsUpdated: result.Updated,
                listingsRemoved: 0,
                ct: ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape/upsert {Agency}", agencyName);

            await logging.FailLogAsync(logId,
                errorMessage: ex.Message,
                errorDetails: ex.ToString(),
                ct: ct);
        }
    }
}
