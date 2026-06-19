using CasaSim.Api;
using CasaSim.Core.Interfaces;
using CasaSim.Scraper.Configuration;
using CasaSim.Scraper.Diagnostics;
using CasaSim.Scraper.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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

        // ── OpenTelemetry ─────────────────────────────────────
        var otelResourceBuilder = ResourceBuilder.CreateDefault()
            .AddService("casasim-scraper", serviceVersion: "1.0.0");

        var otlpCfg = ctx.Configuration.GetSection("OpenTelemetry:Otlp");
        var otlpEndpoint = otlpCfg["Endpoint"];
        var hasOtlpEndpoint =
            !string.IsNullOrWhiteSpace(otlpEndpoint) ||
            !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT"));

        services.AddOpenTelemetry()
            .WithTracing(tracing =>
            {
                tracing
                    .SetResourceBuilder(otelResourceBuilder)
                    .AddSource("CasaSim.Scraper")
                    .AddHttpClientInstrumentation();

                if (hasOtlpEndpoint)
                {
                    tracing.AddOtlpExporter(options =>
                    {
                        // OTLP is configured via standard env vars / config:
                        //   OTEL_EXPORTER_OTLP_ENDPOINT, OTEL_EXPORTER_OTLP_HEADERS, etc.
                        // Optional: override endpoint from config section.
                        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                        {
                            otlpCfg.Bind(options);
                        }
                    });
                }
            })
            .WithMetrics(metrics =>
            {
                metrics
                    .SetResourceBuilder(otelResourceBuilder)
                    .AddMeter("CasaSim.Scraper")
                    .AddHttpClientInstrumentation();

                if (hasOtlpEndpoint)
                {
                    metrics.AddOtlpExporter(options =>
                    {
                        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
                        {
                            otlpCfg.Bind(options);
                        }
                    });
                }
            });
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
