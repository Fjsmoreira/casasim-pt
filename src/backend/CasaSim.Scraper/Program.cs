using CasaSim.Scraper.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        services.AddHttpClient();
        services.AddScoped<RemaxScraper>();
        services.AddHostedService<ScraperOrchestrator>();
    })
    .Build();

await host.RunAsync();
