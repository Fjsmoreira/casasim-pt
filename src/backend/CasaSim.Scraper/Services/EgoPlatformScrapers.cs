using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for eGO Real Estate platform sites.
/// eGO is a common Portuguese real estate website CMS.
/// 
/// Since eGO's listing pages are JavaScript-rendered SPAs, we use the XML sitemap
/// (sitemap-pt-pt.xml) to discover individual property URLs, then attempt to scrape
/// detail pages. Detail pages may also be partially JS-rendered; if parsing fails,
/// we fall back to minimal data from the URL/sitemap.
///
/// Sites using this: imopombal.pt, lionscastles.pt, habifit.pt, cosyimobiliaria.pt
/// </summary>

// ── Concrete scraper classes ──────────────────────────────────────

internal sealed class ImoPombalScraper : IPropertyScraper
{
    public string AgencyName => "ImoPombal";
    private const string BaseUrl = "https://www.imopombal.pt";
    private readonly HttpClient _http;
    private readonly ILogger<ImoPombalScraper> _logger;
    public ImoPombalScraper(HttpClient http, ILogger<ImoPombalScraper> logger) { _http = http; _logger = logger; }
    public Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
        => EgoSitemapScraper.ScrapeAsync(BaseUrl, AgencyName, _http, _logger, ct);
}

internal sealed class LionscastlesScraper : IPropertyScraper
{
    public string AgencyName => "LionsCastles";
    private const string BaseUrl = "https://www.lionscastles.pt";
    private readonly HttpClient _http;
    private readonly ILogger<LionscastlesScraper> _logger;
    public LionscastlesScraper(HttpClient http, ILogger<LionscastlesScraper> logger) { _http = http; _logger = logger; }
    public Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
        => EgoSitemapScraper.ScrapeAsync(BaseUrl, AgencyName, _http, _logger, ct);
}

internal sealed class HabifitScraper : IPropertyScraper
{
    public string AgencyName => "Habifit";
    private const string BaseUrl = "https://www.habifit.pt";
    private readonly HttpClient _http;
    private readonly ILogger<HabifitScraper> _logger;
    public HabifitScraper(HttpClient http, ILogger<HabifitScraper> logger) { _http = http; _logger = logger; }
    public Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
        => EgoSitemapScraper.ScrapeAsync(BaseUrl, AgencyName, _http, _logger, ct);
}

internal sealed class CosyImobiliariaScraper : IPropertyScraper
{
    public string AgencyName => "Cosy Imobiliária";
    private const string BaseUrl = "https://www.cosyimobiliaria.pt";
    private readonly HttpClient _http;
    private readonly ILogger<CosyImobiliariaScraper> _logger;
    public CosyImobiliariaScraper(HttpClient http, ILogger<CosyImobiliariaScraper> logger) { _http = http; _logger = logger; }
    public Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
        => EgoSitemapScraper.ScrapeAsync(BaseUrl, AgencyName, _http, _logger, ct);
}

// ── Shared eGO scraping logic ─────────────────────────────────────

internal static class EgoSitemapScraper
{
    public static async Task<IReadOnlyList<Property>> ScrapeAsync(
        string baseUrl, string agencyName, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var results = new List<Property>();
        try
        {
            // Step 1: Discover property URLs from sitemap
            var urls = await DiscoverFromSitemapAsync(baseUrl, http, logger, ct);
            logger.LogInformation("{Agency}: sitemap found {Count} property URLs", agencyName, urls.Count);

            if (urls.Count == 0)
            {
                // Fallback: try listing page scraping
                urls = await DiscoverFromListingPageAsync(baseUrl, http, logger, ct);
                logger.LogInformation("{Agency}: listing page fallback found {Count} URLs", agencyName, urls.Count);
            }

            // Step 2: Scrape each detail page
            foreach (var (url, slug, numericId) in urls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var prop = await ScrapeDetailAsync(baseUrl, url, slug, numericId, agencyName, http, ct);
                    if (prop is not null) results.Add(prop);
                    await Task.Delay(600, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "{Agency}: failed {Url}", agencyName, url);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Agency}: scrape failed", agencyName);
        }
        logger.LogInformation("{Agency}: scraped {Count} properties", agencyName, results.Count);
        return results;
    }

    private static async Task<List<(string Url, string Slug, string NumericId)>> DiscoverFromSitemapAsync(
        string baseUrl, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var results = new List<(string Url, string Slug, string NumericId)>();

        // Try Portuguese sitemap
        foreach (var sitemapPath in new[] { "/sitemap-pt-pt.xml", "/sitemap.xml" })
        {
            try
            {
                var resp = await http.GetAsync($"{baseUrl}{sitemapPath}", ct);
                if (!resp.IsSuccessStatusCode) continue;

                var xml = await resp.Content.ReadAsStringAsync(ct);
                var doc = XDocument.Parse(xml);

                // Handle both <urlset> (direct URLs) and <sitemapindex> (nested sitemaps)
                XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";

                // Check if it's an index
                var sitemaps = doc.Descendants(ns + "sitemap");
                foreach (var sm in sitemaps)
                {
                    var loc = sm.Element(ns + "loc")?.Value;
                    if (loc is not null && loc.Contains("pt-pt", StringComparison.OrdinalIgnoreCase))
                    {
                        var innerResp = await http.GetAsync(loc, ct);
                        if (innerResp.IsSuccessStatusCode)
                        {
                            var innerXml = await innerResp.Content.ReadAsStringAsync(ct);
                            var innerDoc = XDocument.Parse(innerXml);
                            ExtractPropertyUrls(innerDoc, ns, baseUrl, results);
                        }
                    }
                }

                // Direct URLs
                if (results.Count == 0)
                    ExtractPropertyUrls(doc, ns, baseUrl, results);

                if (results.Count > 0) break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "{Agency}: sitemap {Path} failed", "", sitemapPath);
            }
        }
        return results;
    }

    private static void ExtractPropertyUrls(
        XDocument doc, XNamespace ns, string baseUrl, List<(string Url, string Slug, string NumericId)> results)
    {
        var urls = doc.Descendants(ns + "url");
        foreach (var urlElem in urls)
        {
            var loc = urlElem.Element(ns + "loc")?.Value;
            if (loc is null) continue;

            // eGO property URLs: /imovel/{slug}/{numeric-id}
            var match = Regex.Match(loc, @"/imovel/([^/]+)/(\d+)", RegexOptions.IgnoreCase);
            if (!match.Success) continue;

            var slug = match.Groups[1].Value;
            var numericId = match.Groups[2].Value;

            if (!results.Any(r => r.NumericId == numericId))
                results.Add((loc, slug, numericId));
        }
    }

    private static async Task<List<(string Url, string Slug, string NumericId)>> DiscoverFromListingPageAsync(
        string baseUrl, HttpClient http, ILogger logger, CancellationToken ct)
    {
        // Fallback: try to find property URLs in the listing page HTML
        var results = new List<(string Url, string Slug, string NumericId)>();
        try
        {
            var resp = await http.GetAsync($"{baseUrl}/imoveis", ct);
            if (!resp.IsSuccessStatusCode) return results;
            var html = await resp.Content.ReadAsStringAsync(ct);

            var matches = Regex.Matches(html, @"/imovel/([^/""'\s]+)/(\d+)", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var slug = m.Groups[1].Value;
                var id = m.Groups[2].Value;
                if (!results.Any(r => r.NumericId == id))
                    results.Add(($"{baseUrl}/imovel/{slug}/{id}", slug, id));
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "{Agency}: listing page fallback failed", "");
        }
        return results;
    }

    private static async Task<Property?> ScrapeDetailAsync(
        string baseUrl, string url, string slug, string numericId,
        string agencyName, HttpClient http, CancellationToken ct)
    {
        try
        {
            var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode) return null;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // eGO detail pages are JS-rendered — the HTML may be mostly templates.
            // We extract whatever static data is available and fall back to heuristics.

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Try to get title
            var titleNode = doc.DocumentNode.SelectSingleNode("//title");
            var title = titleNode?.InnerText.Trim() ?? "";

            // eGO pages render properties via Angular/Vue templates.
            // Check for any data in the page (json-ld, meta tags, hidden inputs)
            var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            var description = metaDesc?.GetAttributeValue("content", "") ?? "";

            // ── Heuristic extraction from slug ─────────────────
            // eGO URLs: /imovel/{property-type-slug}/{id}
            // e.g., /imovel/moradia-isolada/12345, /imovel/apartamento/67890
            var propertyType = MapSlugToPropertyType(slug);

            // ── Try extracting structured data from the page ───
            decimal price = 0m;
            int? bedrooms = null;
            double? areaM2 = null;
            string? city = "Pombal";
            var transaction = TransactionType.Sale;
            var images = new List<string>();

            // Try to find price in page text
            var priceMatch = Regex.Match(html, @"(\d[\d\s\.]*)\s*€", RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                var priceText = priceMatch.Groups[1].Value.Replace(" ", "").Replace(".", "");
                if (decimal.TryParse(priceText, out var p)) price = p;
            }

            // Extract images
            var imgMatches = Regex.Matches(html, @"(?:src|href)=\""([^""]*(?:media|image|foto|photo|img)[^""]*\.(?:jpg|jpeg|png|webp)[^""]*)\""", RegexOptions.IgnoreCase);
            foreach (Match m in imgMatches)
            {
                var src = m.Groups[1].Value;
                if (!src.StartsWith("http")) src = baseUrl.TrimEnd('/') + (src.StartsWith("/") ? "" : "/") + src;
                if (!images.Contains(src)) images.Add(src);
            }

            // Build minimal property from available data
            var displayTitle = !string.IsNullOrWhiteSpace(title)
                ? title
                : $"{slug.Replace("-", " ")} em Pombal";

            return new Property
            {
                ExternalId = numericId,
                SourceAgency = agencyName,
                Title = displayTitle,
                Description = description,
                Price = price,
                Currency = "EUR",
                Type = propertyType,
                Transaction = transaction,
                City = city,
                District = "Leiria",
                AreaM2 = areaM2,
                Bedrooms = bedrooms,
                Images = images,
                ListingUrl = url,
                Status = PropertyStatus.Active,
            };
        }
        catch
        {
            // Return minimal property with just URL data
            return new Property
            {
                ExternalId = numericId,
                SourceAgency = agencyName,
                Title = slug.Replace("-", " "),
                Currency = "EUR",
                Type = MapSlugToPropertyType(slug),
                Transaction = TransactionType.Sale,
                City = "Pombal",
                District = "Leiria",
                ListingUrl = url,
                Status = PropertyStatus.Active,
            };
        }
    }

    private static PropertyType MapSlugToPropertyType(string slug)
    {
        var lower = slug.ToLowerInvariant();
        if (lower.Contains("apartamento")) return PropertyType.Apartment;
        if (lower.Contains("moradia") || lower.Contains("vivenda") || lower.Contains("casa")) return PropertyType.House;
        if (lower.Contains("terreno") || lower.Contains("lote") || lower.Contains("rustico") || lower.Contains("agricola")) return PropertyType.Land;
        if (lower.Contains("loja") || lower.Contains("armazem") || lower.Contains("escritorio") ||
            lower.Contains("predio") || lower.Contains("comercio") || lower.Contains("cafe") ||
            lower.Contains("restaurante") || lower.Contains("snack")) return PropertyType.Commercial;
        return PropertyType.Other;
    }
}
