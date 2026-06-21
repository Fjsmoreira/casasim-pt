using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for Moderno Imóveis (modernoimoveis.pt) — WordPress with custom theme.
/// Discovers property URLs from /comprar/ listing page, then scrapes each detail page.
/// </summary>
internal sealed class ModernoImoveisScraper : IPropertyScraper
{
    private const string BaseUrl = "https://www.modernoimoveis.pt";
    public string AgencyName => "Moderno Imóveis";

    private readonly HttpClient _http;
    private readonly ILogger<ModernoImoveisScraper> _logger;

    public ModernoImoveisScraper(HttpClient http, ILogger<ModernoImoveisScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();
        try
        {
            // Discover properties from listing pages
            var urls = await DiscoverPropertiesAsync(ct);
            _logger.LogInformation("{Agency}: discovered {Count} URLs", AgencyName, urls.Count);

            foreach (var url in urls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var prop = await ScrapeDetailAsync(url, ct);
                    if (prop is not null) results.Add(prop);
                    await Task.Delay(600, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "{Agency}: failed {Url}", AgencyName, url);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Agency}: scrape failed", AgencyName);
        }
        _logger.LogInformation("{Agency}: scraped {Count} properties", AgencyName, results.Count);
        return results;
    }

    private async Task<HashSet<string>> DiscoverPropertiesAsync(CancellationToken ct)
    {
        var urls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Source 1: WordPress sitemap (most reliable, catches all posts) ──
        await DiscoverFromWpSitemapAsync(urls, ct);

        // ── Source 2: Listing pages /comprar/, /arrendar/ (catch new posts not yet in sitemap) ──
        foreach (var listingPath in new[] { "/comprar/", "/arrendar/" })
        {
            try
            {
                var resp = await _http.GetAsync($"{BaseUrl}{listingPath}", ct);
                if (!resp.IsSuccessStatusCode) continue;
                var html = await resp.Content.ReadAsStringAsync(ct);

                var doc = new HtmlDocument();
                doc.LoadHtml(html);

                // WordPress property posts have URL pattern like /moradia-t4-ilha-pombal/
                // Find all links that look like property pages (not nav, not wp-admin)
                var links = doc.DocumentNode.SelectNodes("//a[@href]");
                if (links is null) continue;

                foreach (var link in links)
                {
                    var href = link.GetAttributeValue("href", "");
                    if (!href.StartsWith(BaseUrl)) continue;

                    // Skip non-property pages
                    var path = href[BaseUrl.Length..];
                    if (path.StartsWith("/wp-") || path == "/" || path == "/comprar/" ||
                        path == "/arrendar/" || path == "/vender/" || path == "/contactos/" ||
                        path == "/servicos/" || path == "/escritorio" || path == "/termos" ||
                        path == "/remodelacao" || path == "/gestao-de-arrendamento" ||
                        path == "/politica" || path == "/1o-layout" || path == "/2o-layout")
                        continue;

                    // Property URLs like /moradia-t4-ilha-pombal/ have 2 slashes (leading + trailing).
                    // Non-property nav links typically have exactly 1 slash (like /comprar/, /vender/).
                    if (path.Count(c => c == '/') <= 2 && path.Length > 10)
                        urls.Add(href);
                }

                _logger.LogDebug("{Agency}: {Path} → {Count} URLs", AgencyName, listingPath,
                    urls.Count(u => u.Contains(listingPath, StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "{Agency}: failed to fetch {Path}", AgencyName, listingPath);
            }
        }
        return urls;
    }

    /// <summary>
    /// Discover property URLs from WordPress sitemap (wp-sitemap.xml → post sitemaps).
    /// More reliable than scraping listing pages and catches all posts.
    /// </summary>
    private async Task DiscoverFromWpSitemapAsync(HashSet<string> urls, CancellationToken ct)
    {
        try
        {
            // Fetch sitemap index
            var indexXml = await _http.GetStringAsync($"{BaseUrl}/wp-sitemap.xml", ct);
            // Parse post sitemap URLs from index (e.g. wp-sitemap-posts-post-1.xml)
            var postSitemapMatches = Regex.Matches(indexXml,
                @"<loc>\s*(https?://[^<]*/wp-sitemap-posts-post-\d+\.xml)\s*</loc>",
                RegexOptions.IgnoreCase);

            foreach (Match sm in postSitemapMatches)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var sitemapUrl = sm.Groups[1].Value;
                    var sitemapXml = await _http.GetStringAsync(sitemapUrl, ct);

                    // Extract all post URLs from the sitemap
                    var urlMatches = Regex.Matches(sitemapXml,
                        @$"<loc>\s*({Regex.Escape(BaseUrl)}/(?!wp-)[^<]+)\s*</loc>",
                        RegexOptions.IgnoreCase);

                    foreach (Match um in urlMatches)
                    {
                        var url = um.Groups[1].Value;
                        var path = url[BaseUrl.Length..];

                        // Skip known non-property pages
                        if (path is "/" or "/comprar/" or "/arrendar/" or "/vender/" or
                            "/contactos/" or "/servicos/" or "/remodelacao/" or "/termos/" or
                            "/politica/" or "/escritorio/" or "/gestao-de-arrendamento/")
                            continue;

                        urls.Add(url);
                    }

                    _logger.LogDebug("{Agency}: sitemap {SitemapUrl} → {Count} URLs",
                        AgencyName, sitemapUrl, urlMatches.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "{Agency}: failed to process sitemap {SitemapUrl}",
                        AgencyName, sm.Groups[1].Value);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Agency}: wp-sitemap discovery failed, falling back to listing pages",
                AgencyName);
        }
    }

    private async Task<Property?> ScrapeDetailAsync(string url, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // ── Title ──────────────────────────────────────────────
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText.Trim() ?? "";
        // Strip site name suffix: "Property Title - Moderno Imóveis"
        title = Regex.Replace(title, @"\s*[-–|]\s*Moderno Imóveis.*$", "", RegexOptions.IgnoreCase).Trim();

        // ── Description ────────────────────────────────────────
        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        var description = metaDesc?.GetAttributeValue("content", "") ?? "";

        // ── Extract full page text for parsing ─────────────────
        var bodyText = doc.DocumentNode.InnerText;

        // ── Price ──────────────────────────────────────────────
        var price = ExtractPrice(bodyText);

        // ── Property type ──────────────────────────────────────
        var propType = DetectPropertyType(title, description, url);

        // ── Transaction type ───────────────────────────────────
        var transType = bodyText.Contains("arrenda", StringComparison.OrdinalIgnoreCase) ||
                        url.Contains("arrenda", StringComparison.OrdinalIgnoreCase)
            ? TransactionType.Rent
            : TransactionType.Sale;

        // ── Bedrooms ───────────────────────────────────────────
        var bedrooms = ExtractInt(bodyText, @"[Tt](\d+)\b");

        // ── Area ───────────────────────────────────────────────
        var areaM2 = ExtractDouble(bodyText, @"(\d+[\.,]?\d*)\s*m²");

        // ── Location ───────────────────────────────────────────
        var (parish, city) = ExtractLocation(bodyText);

        // ── Images ─────────────────────────────────────────────
        var images = new List<string>();
        var imgNodes = doc.DocumentNode.SelectNodes("//img[contains(@class, 'wp-post-image') or contains(@class, 'attachment') or contains(@src, 'wp-content/uploads')]");
        if (imgNodes is null)
            imgNodes = doc.DocumentNode.SelectNodes("//img[contains(@src, 'wp-content/uploads')]");

        if (imgNodes is not null)
        {
            foreach (var img in imgNodes)
            {
                var src = img.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                {
                    if (!src.StartsWith("http")) src = BaseUrl + (src.StartsWith("/") ? "" : "/") + src;
                    images.Add(src);
                }
            }
        }

        // ── External ID from URL slug ──────────────────────────
        var uri = new Uri(url);
        var slug = uri.AbsolutePath.Trim('/').Split('/').Last();

        return new Property
        {
            ExternalId = slug,
            SourceAgency = AgencyName,
            Title = title,
            Description = description,
            Price = price,
            Currency = "EUR",
            Type = propType,
            Transaction = transType,
            City = city,
            District = "Leiria",
            AreaM2 = areaM2,
            Bedrooms = bedrooms,
            Images = images,
            ListingUrl = url,
            Status = PropertyStatus.Active,
        };
    }

    private static decimal ExtractPrice(string text)
    {
        // Portuguese price patterns: "258.000 €" or "1.250 €"
        var match = Regex.Match(text, @"(\d[\d\s\.]*)\s*€", RegexOptions.IgnoreCase);
        if (!match.Success)
            match = Regex.Match(text, @"€\s*(\d[\d\s\.]*)", RegexOptions.IgnoreCase);
        if (!match.Success) return 0m;

        var cleaned = match.Groups[1].Value.Replace(" ", "").Replace(".", "");
        return decimal.TryParse(cleaned, out var p) ? p : 0m;
    }

    private static int? ExtractInt(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var v)) return v;
        return null;
    }

    private static double? ExtractDouble(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var cleaned = match.Groups[1].Value.Replace(" ", "").Replace(",", ".");
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static PropertyType DetectPropertyType(string title, string description, string url)
    {
        var combined = (title + " " + description + " " + url).ToLowerInvariant();
        if (combined.Contains("apartamento")) return PropertyType.Apartment;
        if (combined.Contains("moradia") || combined.Contains("vivenda")) return PropertyType.House;
        if (combined.Contains("terreno") || combined.Contains("lote")) return PropertyType.Land;
        if (combined.Contains("loja") || combined.Contains("armazém") || combined.Contains("armazem") ||
            combined.Contains("escritório") || combined.Contains("escritorio") || combined.Contains("prédio") ||
            combined.Contains("predio")) return PropertyType.Commercial;
        return PropertyType.Other;
    }

    private static (string? parish, string city) ExtractLocation(string text)
    {
        var city = "Pombal";
        string? parish = null;

        // Common patterns: "em Pombal", "na freguesia de Abiul", "Abiul, Pombal"
        var fregMatch = Regex.Match(text, @"freguesia\s+(?:de\s+)?(\w+(?:\s+\w+)?)", RegexOptions.IgnoreCase);
        if (fregMatch.Success)
            parish = ProperCase(fregMatch.Groups[1].Value);

        var cityMatch = Regex.Match(text, @"\b(Pombal|Leiria|Coimbra|Ansião|Soure|Condeixa)\b", RegexOptions.IgnoreCase);
        if (cityMatch.Success)
            city = ProperCase(cityMatch.Groups[1].Value);

        return (parish, city);
    }

    private static string ProperCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        var parts = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(" ", parts.Select(p =>
            char.ToUpperInvariant(p[0]) + p[1..].ToLowerInvariant()));
    }
}
