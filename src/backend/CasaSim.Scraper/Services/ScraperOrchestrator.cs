using CasaSim.Core.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

internal sealed class ScraperOrchestrator : BackgroundService
{
    private readonly ILogger<ScraperOrchestrator> _logger;

    public ScraperOrchestrator(ILogger<ScraperOrchestrator> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scraper orchestrator started");

        while (!stoppingToken.IsCancellationRequested)
        {
            // TODO: run each registered scraper and persist results
            _logger.LogInformation("Scrape cycle at {Time}", DateTimeOffset.UtcNow);
            await Task.Delay(TimeSpan.FromHours(6), stoppingToken);
        }
    }
}
