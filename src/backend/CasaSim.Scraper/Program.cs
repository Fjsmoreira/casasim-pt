using CasaSim.Api;
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

        // Scraper services
        services.AddScoped<ScrapeLoggingService>();
        services.AddScoped<RemaxScraper>();
        services.AddScoped<RemaxListingParser>();
        services.AddScoped<ListingUpsertService>();

        // Background orchestrator
        services.AddHostedService<ScraperOrchestrator>();
    })
    .ConfigureLogging(logging =>
    {
        logging.AddConsole();
        logging.SetMinimumLevel(LogLevel.Information);
    })
    .Build();

await host.RunAsync();
