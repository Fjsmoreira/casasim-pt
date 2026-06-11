using CasaSim.Api;
using CasaSim.Core.Interfaces;
using CasaSim.Scraper.Configuration;
using CasaSim.Scraper.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        // HTTP client
        services.AddHttpClient();

        // Database
        var connStr = ctx.Configuration.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connStr))
        {
            services.AddDbContext<AppDbContext>(options =>
            {
                options.UseNpgsql(connStr, npgsql =>
                    npgsql.UseNetTopologySuite());
                options.UseSnakeCaseNamingConvention();
            });
        }

        // Scraper source configuration (per-source interval / enabled)
        services.Configure<Dictionary<string, ScraperSourceOptions>>(
            ctx.Configuration.GetSection("ScraperSources"));

        // Scraper services
        services.AddScoped<ScrapeLoggingService>();
        services.AddScoped<IPropertyScraper, RemaxScraper>();
        services.AddScoped<IPropertyScraper, Century21Scraper>();
        services.AddScoped<RemaxListingParser>();
        services.AddScoped<Century21ListingParser>();
        services.AddScoped<ListingUpsertService>();

        // Background orchestrator (PeriodicTimer-based)
        services.AddHostedService<ScraperOrchestrator>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

await host.RunAsync();
