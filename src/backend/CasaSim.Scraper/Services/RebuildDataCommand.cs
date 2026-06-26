using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Core.Interfaces;
using CasaSim.Scraper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasaSim.Scraper.Services;

internal static class RebuildDataCommand
{
    public static async Task<int> RunAsync(IHost host, CancellationToken ct = default)
    {
        var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("RebuildData");

        await using (var scope = host.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            logger.LogWarning("Dropping and recreating database via migrations");
            await db.Database.EnsureDeletedAsync(ct);
            await db.Database.MigrateAsync(ct);
        }

        var failedScrapers = 0;
        var scrapers = host.Services.GetServices<IPropertyScraper>().ToList();
        foreach (var scraper in scrapers)
        {
            await using var scope = host.Services.CreateAsyncScope();
            var upsert = scope.ServiceProvider.GetRequiredService<ListingUpsertService>();
            var agencySlug = ListingUpsertService.ResolveAgencySlug(scraper.AgencyName);
            if (agencySlug is null)
            {
                logger.LogWarning("Skipping scraper {Agency}: no agency slug mapping", scraper.AgencyName);
                failedScrapers++;
                continue;
            }

            try
            {
                logger.LogInformation("Running full scrape for {Agency}", scraper.AgencyName);
                var properties = await scraper.ScrapeAsync(ct);
                var result = await upsert.UpsertBatchAsync(properties, agencySlug, ct: ct);
                var seen = properties
                    .Select(p => p.ExternalId)
                    .Where(id => !string.IsNullOrWhiteSpace(id))
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);
                var removed = await upsert.MarkMissingListingsRemovedAsync(agencySlug, seen, ct: ct);
                await scope.ServiceProvider.GetRequiredService<AppDbContext>().SaveChangesAsync(ct);
                logger.LogInformation(
                    "{Agency}: {Found} found, {Created} created, {Updated} updated, {Skipped} skipped, {Removed} removed",
                    scraper.AgencyName,
                    properties.Count,
                    result.Created,
                    result.Updated,
                    result.Skipped,
                    removed);
            }
            catch (Exception ex)
            {
                failedScrapers++;
                logger.LogError(ex, "Scraper {Agency} failed during rebuild", scraper.AgencyName);
            }
        }

        var failedAi = await RunAiUntilSettledAsync(host, logger, ct);
        await PrintCountsAsync(host, logger, failedScrapers, failedAi, ct);
        return failedScrapers == 0 && failedAi == 0 ? 0 : 1;
    }

    private static async Task<int> RunAiUntilSettledAsync(IHost host, ILogger logger, CancellationToken ct)
    {
        var ai = host.Services.GetServices<IHostedService>().OfType<AiEnrichmentService>().FirstOrDefault();
        var options = host.Services.GetRequiredService<IOptions<AiOptions>>().Value;
        if (!options.Enabled)
        {
            logger.LogInformation("AI enrichment is disabled; skipping AI rebuild step");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(options.ResolveApiKey()))
        {
            logger.LogWarning("AI enrichment is enabled but no API key is configured; skipping AI rebuild step");
            return 0;
        }

        if (ai is null)
        {
            logger.LogWarning("AI enrichment service is not registered; skipping AI rebuild step");
            return 0;
        }

        var previousProcessed = -1;
        for (var iteration = 0; iteration < 500; iteration++)
        {
            await ai.ProcessBatchAsync(ct);

            await using var scope = host.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var active = await db.Listings.CountAsync(l => l.Status == ListingStatus.Active, ct);
            var processed = await db.ListingAiEnrichments.CountAsync(e =>
                e.Listing != null &&
                e.Listing.Status == ListingStatus.Active &&
                (e.Status == ListingAiEnrichmentStatus.Succeeded || e.Status == ListingAiEnrichmentStatus.Failed), ct);

            if (processed >= active || processed == previousProcessed)
                break;

            previousProcessed = processed;
        }

        await using var finalScope = host.Services.CreateAsyncScope();
        var finalDb = finalScope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await finalDb.ListingAiEnrichments.CountAsync(e =>
            e.Listing != null &&
            e.Listing.Status == ListingStatus.Active &&
            e.Status == ListingAiEnrichmentStatus.Failed, ct);
    }

    private static async Task PrintCountsAsync(IHost host, ILogger logger, int failedScrapers, int failedAi, CancellationToken ct)
    {
        await using var scope = host.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var byAgency = await db.Agencies
            .AsNoTracking()
            .Select(a => new
            {
                a.Name,
                ActiveListings = a.Listings.Count(l => l.Status == ListingStatus.Active),
            })
            .OrderBy(a => a.Name)
            .ToListAsync(ct);

        foreach (var row in byAgency)
            logger.LogInformation("{Agency}: {ActiveListings} active listing(s)", row.Name, row.ActiveListings);

        logger.LogInformation("Failed scraper count: {FailedScrapers}", failedScrapers);
        logger.LogInformation("Failed AI count: {FailedAi}", failedAi);
    }
}
