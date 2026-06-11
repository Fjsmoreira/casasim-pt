using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

internal sealed class RemaxScraper : IPropertyScraper
{
    public string AgencyName => "Remax";

    private readonly HttpClient _http;
    private readonly ILogger<RemaxScraper> _logger;

    public RemaxScraper(HttpClient http, ILogger<RemaxScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scraping {Agency} listings for Pombal", AgencyName);

        // TODO: implement Remax search page parsing
        // URL pattern: https://www.remax.pt/en/search?city=Pombal&transactionType=...
        var response = await _http.GetAsync(
            "https://www.remax.pt/en/search?city=Pombal&transactionType=sale&pageSize=48",
            ct);

        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync(ct);
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // Parse listing cards from the HTML
        var properties = new List<Property>();
        // TODO: parse cards with HtmlAgilityPack selectors

        _logger.LogInformation("Found {Count} properties from {Agency}", properties.Count, AgencyName);
        return properties;
    }
}
