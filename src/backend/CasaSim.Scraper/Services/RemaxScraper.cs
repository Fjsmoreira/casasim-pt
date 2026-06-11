using System.Text.RegularExpressions;
using System.Xml;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Remax Pombal scraper — discovers listing URLs via the RE/MAX Portugal
/// sitemap, then fetches each detail page and parses it with
/// <see cref="RemaxListingParser"/>.
///
/// Remax uses a Next.js SPA for its search pages — listing data is loaded
/// client-side via API calls, making plain-HTML search page scraping
/// unreliable.  Instead we use the public listing sitemaps
/// (https://remax.pt/sitemap/listings_details_{lang}_*.xml.gz), filter for
/// Pombal, and fetch each detail page individually.
///
/// The scraper covers three categories:
///   - Sale (Venda)       — businessTypeID=1
///   - Rent (Arrendamento) — businessTypeID=2
///   - Commercial          — listingClassID=2 (both business types)
/// </summary>
internal sealed class RemaxScraper : IPropertyScraper
{
    public string AgencyName => "Remax";

    private const string SitemapIndexUrl = "https://remax.pt/sitemap.xml";
    private const string ListingDetailSitemapPattern = "listings_details_pt_";
    private const string PombalFilterPattern = @"pombal"; // case-insensitive

    private readonly HttpClient _http;
    private readonly RemaxListingParser _parser;
    private readonly ILogger<RemaxScraper> _logger;

    public RemaxScraper(HttpClient http, RemaxListingParser parser, ILogger<RemaxScraper> logger)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        _logger.LogInformation("Scraping {Agency} listings for Pombal", AgencyName);

        var results = new List<Property>();

        try
        {
            // Step 1: Discover listing URLs from the sitemap
            var listingUrls = await DiscoverPombalListingUrlsAsync(ct);

            _logger.LogInformation(
                "Discovered {Count} potential Pombal listing URLs from sitemaps",
                listingUrls.Count);

            // Step 2: Fetch and parse each detail page
            foreach (var url in listingUrls)
            {
                try
                {
                    var property = await FetchAndParseDetailAsync(url, ct);
                    if (property is not null)
                        results.Add(property);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to fetch/parse Remax listing at {Url}", url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape Remax Pombal listings");
        }

        _logger.LogInformation(
            "Found {Count} properties from {Agency}",
            results.Count,
            AgencyName);

        return results;
    }

    // ── Sitemap-based URL discovery ─────────────────────────────

    /// <summary>
    /// Download the sitemap index, find listing detail sitemaps, then
    /// extract all Pombal listing URLs from them.
    /// </summary>
    private async Task<List<string>> DiscoverPombalListingUrlsAsync(CancellationToken ct)
    {
        var listingUrls = new List<string>();

        // Step 1: Get the sitemap index
        var sitemapIndexXml = await FetchStringAsync(SitemapIndexUrl, ct);
        if (string.IsNullOrEmpty(sitemapIndexXml))
        {
            _logger.LogWarning("Failed to fetch sitemap index at {Url}", SitemapIndexUrl);
            return listingUrls;
        }

        // Step 2: Find all listing detail sitemap URLs
        var detailSitemapUrls = ExtractSitemapUrls(sitemapIndexXml, ListingDetailSitemapPattern);

        _logger.LogInformation(
            "Found {Count} listing detail sitemaps to scan",
            detailSitemapUrls.Count);

        // Step 3: Scan each sitemap for Pombal listings
        foreach (var sitemapUrl in detailSitemapUrls)
        {
            try
            {
                var sitemapXml = await FetchStringAsync(sitemapUrl, ct);
                if (string.IsNullOrEmpty(sitemapXml))
                    continue;

                var urls = ExtractListingUrls(sitemapXml, PombalFilterPattern);
                listingUrls.AddRange(urls);

                _logger.LogDebug(
                    "Sitemap {Sitemap}: found {Count} Pombal URLs",
                    sitemapUrl.Split('/').LastOrDefault(),
                    urls.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process sitemap {SitemapUrl}", sitemapUrl);
            }
        }

        // Deduplicate (a listing could appear in both EN and PT sitemaps,
        // or across multiple sitemap files)
        listingUrls = listingUrls.Distinct().ToList();

        return listingUrls;
    }

    /// <summary>
    /// Fetch a URL as a string, accepting gzipped content transparently.
    /// </summary>
    private async Task<string?> FetchStringAsync(string url, CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            // ReadAsStringAsync handles gzip transparently when
            // AutomaticDecompression is configured on HttpClient
            return await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch {Url}", url);
            return null;
        }
    }

    /// <summary>
    /// Extract all &lt;loc&gt; URLs from a sitemap XML that match the given
    /// pattern (case-insensitive).
    /// </summary>
    private static List<string> ExtractSitemapUrls(string sitemapXml, string urlFilter)
    {
        var urls = new List<string>();
        var doc = new HtmlDocument();
        doc.LoadHtml(sitemapXml);

        var locNodes = doc.DocumentNode.SelectNodes("//loc");
        if (locNodes is null)
            return urls;

        foreach (var node in locNodes)
        {
            var loc = node.InnerText.Trim();
            if (!string.IsNullOrEmpty(loc) &&
                loc.Contains(urlFilter, StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(loc);
            }
        }

        return urls;
    }

    /// <summary>
    /// Extract listing URLs from a sitemap XML, filtering for Pombal
    /// in the URL slug.
    /// </summary>
    private static List<string> ExtractListingUrls(string sitemapXml, string filter)
    {
        var urls = new List<string>();
        var doc = new HtmlDocument();
        doc.LoadHtml(sitemapXml);

        var locNodes = doc.DocumentNode.SelectNodes("//loc");
        if (locNodes is null)
            return urls;

        foreach (var node in locNodes)
        {
            var loc = node.InnerText.Trim();
            if (!string.IsNullOrEmpty(loc) &&
                loc.Contains(filter, StringComparison.OrdinalIgnoreCase))
            {
                urls.Add(loc);
            }
        }

        return urls;
    }

    // ── Detail page fetching and parsing ─────────────────────────

    /// <summary>
    /// Fetch a Remax detail page and parse it with <see cref="RemaxListingParser"/>.
    /// Returns a <see cref="Property"/> or null on failure.
    /// </summary>
    private async Task<Property?> FetchAndParseDetailAsync(string detailUrl, CancellationToken ct)
    {
        _logger.LogDebug("Fetching detail page: {Url}", detailUrl);

        var response = await _http.GetAsync(detailUrl, ct);
        response.EnsureSuccessStatusCode();

        var html = await response.Content.ReadAsStringAsync(ct);

        // Parse with the existing parser
        var parsed = _parser.ParseFromHtml(html, detailUrl);
        if (parsed is null)
        {
            _logger.LogWarning("Failed to parse Remax listing from {Url}", detailUrl);
            return null;
        }

        return parsed;
    }
}
