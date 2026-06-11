using CasaSim.Api;
using CasaSim.Core.Interfaces;
using CasaSim.Scraper.Configuration;
using CasaSim.Scraper.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Formatting.Json;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console(new JsonFormatter(renderMessage: true))
    .CreateBootstrapLogger();

try
{
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
        services.AddScoped<IPropertyScraper, EraScraper>();
        services.AddScoped<RemaxListingParser>();
        services.AddScoped<Century21ListingParser>();
        services.AddScoped<EraListingParser>();
        services.AddScoped<ListingUpsertService>();

        // Background orchestrator (PeriodicTimer-based)
        services.AddHostedService<ScraperOrchestrator>();
    })
    .UseSerilog((ctx, lc) => lc
        .ReadFrom.Configuration(ctx.Configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("service.name", "casasim-scraper")
        .Enrich.WithProperty("environment", ctx.HostingEnvironment.EnvironmentName)
        .WriteTo.Console(new JsonFormatter(renderMessage: true)))
    .Build();

await host.RunAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Scraper terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
