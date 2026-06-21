using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;
using Microsoft.Playwright;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for eGO Real Estate platform sites.
/// eGO is a common Portuguese real estate website CMS.
/// 
/// eGO listing pages are JavaScript-rendered SPAs with zero property links
/// in static HTML. Sitemaps contain only navigation pages — no property URLs.
/// The eGO API (websiteapi.egorealestate.com) requires authenticated sessions
/// and blocks non-browser access.
///
/// This scraper uses Playwright (headless Chromium) to load listing pages
/// with JS execution, then extracts property links from the rendered DOM.
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
    private static IBrowser? _sharedBrowser;
    private static readonly SemaphoreSlim _browserLock = new(1, 1);

    public static async Task<IReadOnlyList<Property>> ScrapeAsync(
        string baseUrl, string agencyName, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var results = new List<Property>();
        try
        {
            // Step 1: Discover property URLs
            // Try Playwright first (JS-rendered listing pages), fall back to sitemap/listing HTML
            var urls = await DiscoverViaPlaywrightAsync(baseUrl, agencyName, logger, ct);
            
            if (urls.Count == 0)
            {
                logger.LogInformation("{Agency}: Playwright discovery found 0 URLs, trying fallback", agencyName);
                urls = await DiscoverFromSitemapAsync(baseUrl, http, logger, ct);
                logger.LogInformation("{Agency}: sitemap fallback found {Count} property URLs", agencyName, urls.Count);

                if (urls.Count == 0)
                {
                    urls = await DiscoverFromListingPageAsync(baseUrl, http, logger, ct);
                    logger.LogInformation("{Agency}: listing page fallback found {Count} URLs", agencyName, urls.Count);
                }
            }
            else
            {
                logger.LogInformation("{Agency}: Playwright found {Count} property URLs", agencyName, urls.Count);
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

    /// <summary>
    /// Discover property URLs using Playwright headless browser.
    /// Loads the listing page with JavaScript execution, waits for property
    /// cards to render, then extracts property detail page URLs.
    /// </summary>
    private static async Task<List<(string Url, string Slug, string NumericId)>> DiscoverViaPlaywrightAsync(
        string baseUrl, string agencyName, ILogger logger, CancellationToken ct)
    {
        var results = new List<(string Url, string Slug, string NumericId)>();
        
        // Listing pages to try (some sites use /imoveis, others may use different paths)
        var listingPaths = new[] { "/imoveis", "/properties", "/inmuebles", "/biens-immobiliers" };

        try
        {
            var browser = await GetSharedBrowserAsync();
            var context = await browser.NewContextAsync(new BrowserNewContextOptions
            {
                UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/132.0.0.0 Safari/537.36",
                ViewportSize = new ViewportSize { Width = 1920, Height = 1080 },
                JavaScriptEnabled = true
            });

            try
            {
                foreach (var listingPath in listingPaths)
                {
                    if (ct.IsCancellationRequested) break;
                    if (results.Count > 0) break; // got URLs from a previous path

                    var page = await context.NewPageAsync();
                    try
                    {
                        var listingUrl = $"{baseUrl}{listingPath}";
                        logger.LogDebug("{Agency}: Playwright loading {Url}", agencyName, listingUrl);

                        // Navigate and wait for DOM content
                        var resp = await page.GotoAsync(listingUrl, new PageGotoOptions
                        {
                            WaitUntil = WaitUntilState.DOMContentLoaded,
                            Timeout = 20_000
                        });

                        if (resp is null || resp.Status is < 200 or >= 400)
                        {
                            logger.LogDebug("{Agency}: Playwright got status {Status} for {Url}",
                                agencyName, resp?.Status, listingUrl);
                            continue;
                        }

                        // Wait for property cards to render (EGO uses .DataView container)
                        try
                        {
                            await page.WaitForSelectorAsync(".DataView", new PageWaitForSelectorOptions
                            {
                                Timeout = 20_000
                            });
                        }
                        catch (TimeoutException)
                        {
                            logger.LogDebug("{Agency}: Playwright .DataView timeout on {Url}, trying any content",
                                agencyName, listingUrl);
                        }

                        // Extra wait for JavaScript data loading (EGO fetches from API)
                        await Task.Delay(5_000, ct);

                        // Extract property URLs from the rendered DOM
                        // EGO property detail URLs typically look like:
                        //   /imovel/<slug>/<numeric-id>
                        //   /pt-pt/imovel/<slug>/<numeric-id>  
                        var urls = await page.EvaluateAsync<string[]>(
                            @"() => {
                                const links = document.querySelectorAll('a[href]');
                                const patterns = [/\/imovel\//i, /\/property\//i, /\/propriedade\//i];
                                const results = [];
                                for (const a of links) {
                                    for (const p of patterns) {
                                        if (p.test(a.href)) {
                                            results.push(a.href);
                                            break;
                                        }
                                    }
                                }
                                return [...new Set(results)];
                            }");

                        // Parse URLs into (url, slug, numericId) tuples
                        foreach (var url in urls)
                        {
                            if (ct.IsCancellationRequested) break;
                            var parsed = ParseEgoPropertyUrl(url);
                            if (parsed is not null && !results.Any(r => r.NumericId == parsed.Value.NumericId))
                                results.Add(parsed.Value);
                        }

                        logger.LogDebug("{Agency}: Playwright {Path} → {Count} property URLs",
                            agencyName, listingPath, results.Count);
                    }
                    catch (Exception ex)
                    {
                        logger.LogDebug(ex, "{Agency}: Playwright failed on {Path}", agencyName, listingPath);
                    }
                    finally
                    {
                        await page.CloseAsync();
                    }
                }
            }
            finally
            {
                // Close context but keep browser for reuse
                await context.CloseAsync();
            }
        }
        catch (Exception ex)
        {
            // Playwright not installed or browser launch failed — fall back to sitemap/HTML
            logger.LogDebug(ex, "{Agency}: Playwright unavailable, falling back to sitemap/HTML discovery",
                agencyName);
        }

        return results;
    }

    /// <summary>
    /// Parse an eGO property detail URL into (url, slug, numericId).
    /// Expected format: /imovel/<slug>/<numeric-id> or /pt-pt/imovel/<slug>/<numeric-id>
    /// </summary>
    private static (string Url, string Slug, string NumericId)? ParseEgoPropertyUrl(string url)
    {
        // Match: /imovel/<slug>/<numeric-id>
        var match = Regex.Match(url,
            @"(?<base>https?://[^/]+)(?:/[a-z]{2}-[a-z]{2})?/imovel/(?<slug>[^/]+)/(?<id>\d+)",
            RegexOptions.IgnoreCase);
        
        if (!match.Success)
        {
            // Try alternate pattern: /property/<slug>-<id> or /property/<id>
            match = Regex.Match(url,
                @"(?<base>https?://[^/]+)/property/(?<slug>[^/]+)-(?<id>\d+)$",
                RegexOptions.IgnoreCase);
        }

        if (match.Success)
            return (match.Value, match.Groups["slug"].Value, match.Groups["id"].Value);

        return null;
    }

    /// <summary>
    /// Get or create a shared Playwright browser instance.
    /// Reusing the browser across scrapers avoids cold-start overhead.
    /// </summary>
    private static async Task<IBrowser> GetSharedBrowserAsync()
    {
        if (_sharedBrowser?.IsConnected == true) return _sharedBrowser;

        await _browserLock.WaitAsync();
        try
        {
            if (_sharedBrowser?.IsConnected == true) return _sharedBrowser;

            var playwright = await Playwright.CreateAsync();
            _sharedBrowser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-gpu", "--disable-dev-shm-usage" }
            });

            return _sharedBrowser;
        }
        finally
        {
            _browserLock.Release();
        }
    }

    // ── Fallback discovery methods (unchanged from original) ────────

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

                // ── Regex extraction from raw XML ──────────────
                // More reliable than XDocument namespace handling
                // across different .NET runtimes and XML configs.
                var matches = Regex.Matches(xml,
                    @"<loc>\s*(https?://[^<]*/imovel/([^/]+)/(\d+))\s*</loc>",
                    RegexOptions.IgnoreCase);
                foreach (Match m in matches)
                {
                    var url = m.Groups[1].Value;
                    var slug = m.Groups[2].Value;
                    var numericId = m.Groups[3].Value;
                    if (!results.Any(r => r.Item3 == numericId))
                        results.Add((url, slug, numericId));
                }

                if (results.Count > 0) break;
            }
            catch (Exception ex)
            {
                logger.LogDebug(ex, "{Agency}: sitemap {Path} failed", "", sitemapPath);
            }
        }
        return results;
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
                if (!results.Any(r => r.Item3 == id))
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

            // Check for any data in the page (json-ld, meta tags, hidden inputs)
            var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
            var description = metaDesc?.GetAttributeValue("content", "") ?? "";

            // ── Heuristic extraction from slug ─────────────────
            var propertyType = MapSlugToPropertyType(slug);
            // eGO slugs often contain the city: "apartamento-t1-bela-vista-residences-pombal"
            var city = ExtractCityFromSlug(slug) ?? "Pombal";

            // ── Try extracting structured data from the page ───
            decimal price = 0m;
            int? bedrooms = null;
            double? areaM2 = null;
            var transaction = TransactionType.Sale;
            var images = new List<string>();

            // Try to find price in page text
            var priceMatch = Regex.Match(html, @"(\d[\d\s\.]*)\s*€", RegexOptions.IgnoreCase);
            if (priceMatch.Success)
            {
                var priceText = priceMatch.Groups[1].Value.Replace(" ", "").Replace(".", "");
                if (decimal.TryParse(priceText, out var p)) price = p;
            }

            // Extract images using HtmlAgilityPack (more reliable than regex)
            // 1. Find all <img> tags with real image sources (skip base64 placeholders)
            var imgNodes = doc.DocumentNode.SelectNodes("//img[not(contains(@src, 'data:image'))]");
            if (imgNodes is not null)
            {
                foreach (var img in imgNodes)
                {
                    var src = img.GetAttributeValue("src", "");
                    if (string.IsNullOrWhiteSpace(src)) continue;
                    // Only keep actual image URLs (skip JS/CSS assets from egorealestate CDN)
                    if (src.EndsWith(".js", StringComparison.OrdinalIgnoreCase) ||
                        src.EndsWith(".css", StringComparison.OrdinalIgnoreCase) ||
                        src.EndsWith(".svg", StringComparison.OrdinalIgnoreCase))
                        continue;
                    if (!src.StartsWith("http")) src = "https:" + (src.StartsWith("//") ? "" : "//") + src;
                    if (!images.Contains(src)) images.Add(src);
                }
            }

            // 2. Check <picture>/<source> elements for responsive images
            var sourceNodes = doc.DocumentNode.SelectNodes("//source[@srcset or @src]");
            if (sourceNodes is not null)
            {
                foreach (var source in sourceNodes)
                {
                    var srcset = source.GetAttributeValue("srcset", "");
                    var src = source.GetAttributeValue("src", "");
                    var url = !string.IsNullOrWhiteSpace(srcset) ? srcset.Split(',')[0].Trim().Split(' ')[0] : src;
                    if (string.IsNullOrWhiteSpace(url)) continue;
                    if (!url.StartsWith("http")) url = "https:" + (url.StartsWith("//") ? "" : "//") + url;
                    if (!images.Contains(url)) images.Add(url);
                }
            }

            // 3. Also check og:image meta tag (EGO sites always have this)
            var ogImageNode = doc.DocumentNode.SelectSingleNode("//meta[@property='og:image']");
            if (ogImageNode is not null)
            {
                var ogSrc = ogImageNode.GetAttributeValue("content", "");
                if (!string.IsNullOrWhiteSpace(ogSrc))
                {
                    if (!ogSrc.StartsWith("http")) ogSrc = "https:" + (ogSrc.StartsWith("//") ? "" : "//") + ogSrc;
                    if (!images.Contains(ogSrc)) images.Insert(0, ogSrc);
                }
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
            var fallbackCity = ExtractCityFromSlug(slug) ?? "Pombal";
            return new Property
            {
                ExternalId = numericId,
                SourceAgency = agencyName,
                Title = slug.Replace("-", " "),
                Currency = "EUR",
                Type = MapSlugToPropertyType(slug),
                Transaction = TransactionType.Sale,
                City = fallbackCity,
                District = "Leiria",
                ListingUrl = url,
                Status = PropertyStatus.Active,
            };
        }
    }

    /// <summary>
    /// Extract the city name from an eGO URL slug.
    /// eGO slugs often end with the city: "apartamento-t1-bela-vista-residences-pombal"
    /// The heuristic: take the last segment after the final hyphen as the city.
    /// Returns null if the last segment doesn't look like a city name.
    /// </summary>
    private static string? ExtractCityFromSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug)) return null;
        var parts = slug.Split('-');
        // Take the last part as the candidate city
        var last = parts[^1].Trim();
        // Skip if it looks like a numeric ID or too short
        if (last.Length < 3 || int.TryParse(last, out _)) return null;
        // Skip common non-city suffixes
        var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "t0", "t1", "t2", "t3", "t4", "t5", "t6", "duplex",
            "residences", "residence", "novo", "nova", "usado", "usada"
        };
        if (skipWords.Contains(last)) return null;
        return last;
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
