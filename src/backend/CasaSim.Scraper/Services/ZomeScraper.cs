using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for Zome (zome.pt) — Nuxt/Vue.js based site.
/// Target: /pt/leiria-h40157/imoveis (Leiria district listings).
/// Nuxt pages embed server-side data in window.__NUXT__ or __NUXT__ script tag.
/// </summary>
internal sealed class ZomeScraper : IPropertyScraper
{
    private const string BaseUrl = "https://www.zome.pt";
    private const string ListingsPath = "/pt/leiria-h40157/imoveis";
    public string AgencyName => "Zome";

    private readonly HttpClient _http;
    private readonly ILogger<ZomeScraper> _logger;

    public ZomeScraper(HttpClient http, ILogger<ZomeScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();
        try
        {
            // Zome loads listing data via __NUXT__ SSR state (large JSON payload).
            var nuxtData = await FetchNuxtDataAsync(ListingsPath, ct);

            if (nuxtData is not null)
            {
                results.AddRange(ParseNuxtListings(nuxtData));
            }
            else
            {
                // Fallback: scrape from listing page HTML
                var urls = await DiscoverFromHtmlAsync(ListingsPath, ct);
                _logger.LogInformation("{Agency}: HTML fallback found {Count} URLs", AgencyName, urls.Count);

                foreach (var (url, id) in urls)
                {
                    if (ct.IsCancellationRequested) break;
                    try
                    {
                        var prop = await ScrapeDetailAsync(url, id, ct);
                        if (prop is not null) results.Add(prop);
                        await Task.Delay(700, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "{Agency}: failed {Url}", AgencyName, url);
                    }
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

    private async Task<string?> FetchNuxtDataAsync(string path, CancellationToken ct)
    {
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}{path}", ct);
            if (!resp.IsSuccessStatusCode) return null;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Nuxt 2: window.__NUXT__ = {...}
            var match = Regex.Match(html, @"window\.__NUXT__\s*=\s*({.*?});\s*(?:</script>|function)", RegexOptions.Singleline);
            if (match.Success)
                return match.Groups[1].Value;

            // Nuxt 3: <script>window.__NUXT__={...}</script>
            match = Regex.Match(html, @"<script[^>]*>\s*window\.__NUXT__\s*=\s*({.*?})\s*</script>", RegexOptions.Singleline);
            if (match.Success)
                return match.Groups[1].Value;

            // Alternative: id="__NUXT_DATA__"
            match = Regex.Match(html, @"<script[^>]*id=""__NUXT_DATA__""[^>]*>\s*(.*?)\s*</script>", RegexOptions.Singleline);
            if (match.Success)
                return match.Groups[1].Value;

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Agency}: Nuxt data fetch failed", AgencyName);
            return null;
        }
    }

    private List<Property> ParseNuxtListings(string nuxtJson)
    {
        var results = new List<Property>();

        try
        {
            // Zome listing structure in __NUXT__:
            // The data typically contains listing cards with properties like:
            // id, title, price, url, image, propertyType, area, bedrooms, bathrooms, location

            // Extract listing IDs: "id":123456 or "propertyId":"ABC123"
            var idMatches = Regex.Matches(nuxtJson, @"""id"":\s*(\d+)", RegexOptions.IgnoreCase);
            var seenIds = new HashSet<string>();

            foreach (Match idMatch in idMatches)
            {
                var id = idMatch.Groups[1].Value;
                if (!seenIds.Add(id)) continue;

                // Try to find associated data near this ID
                var idx = idMatch.Index;
                var context = nuxtJson.Substring(Math.Max(0, idx - 100), Math.Min(2000, nuxtJson.Length - Math.Max(0, idx - 100)));

                var title = ExtractJsonString(context, @"(?:title|name)"":\s*""([^""]+)");
                var priceStr = ExtractJsonString(context, @"(?:price|preco|preço|salePrice)"":\s*(\d+[\.,]?\d*)");
                var price = ParsePrice(priceStr);
                var url = ExtractJsonString(context, @"(?:url|slug|path)"":\s*""([^""]+)");
                var areaStr = ExtractJsonString(context, @"(?:area|grossArea|usefulArea|landArea)"":\s*(\d+[\.,]?\d*)");
                double? areaM2 = null;
                if (areaStr is not null)
                {
                    areaStr = areaStr.Replace(",", ".");
                    if (double.TryParse(areaStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var a))
                        areaM2 = a;
                }
                var bedroomsStr = ExtractJsonString(context, @"(?:bedrooms|bedroom|quartos)"":\s*(\d+)");
                int? bedrooms = bedroomsStr is not null && int.TryParse(bedroomsStr, out var b) ? b : null;

                // Property type
                var propType = PropertyType.Other;
                if (context.Contains("apartamento", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Apartment;
                else if (context.Contains("moradia", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.House;
                else if (context.Contains("terreno", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Land;
                else if (context.Contains("loja", StringComparison.OrdinalIgnoreCase) || context.Contains("comercial", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Commercial;

                var transType = context.Contains("arrenda", StringComparison.OrdinalIgnoreCase) ? TransactionType.Rent : TransactionType.Sale;

                // Images
                var images = new List<string>();
                var imgMatches = Regex.Matches(context, @"https?://[^""\s]+\.(?:jpg|jpeg|png|webp)", RegexOptions.IgnoreCase);
                foreach (Match im in imgMatches)
                    if (!images.Contains(im.Value)) images.Add(im.Value);

                // Location
                var city = "Leiria";
                if (context.Contains("Pombal", StringComparison.OrdinalIgnoreCase)) city = "Pombal";
                else if (context.Contains("Coimbra", StringComparison.OrdinalIgnoreCase)) city = "Coimbra";

                results.Add(new Property
                {
                    ExternalId = id,
                    SourceAgency = AgencyName,
                    Title = title ?? $"Propriedade {id}",
                    Price = price,
                    Currency = "EUR",
                    Type = propType,
                    Transaction = transType,
                    City = city,
                    District = "Leiria",
                    AreaM2 = areaM2,
                    Bedrooms = bedrooms,
                    Images = images,
                    ListingUrl = url is not null && url.StartsWith("http") ? url : $"{BaseUrl}{url ?? $"/pt/imovel/{id}"}",
                    Status = PropertyStatus.Active,
                });
            }

            _logger.LogInformation("{Agency}: parsed {Count} from Nuxt state", AgencyName, results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Agency}: Nuxt parsing failed", AgencyName);
        }

        return results;
    }

    private async Task<List<(string Url, string Id)>> DiscoverFromHtmlAsync(string path, CancellationToken ct)
    {
        var results = new List<(string, string)>();
        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}{path}", ct);
            if (!resp.IsSuccessStatusCode) return results;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Zome detail URLs: /pt/imovel/{id} or /pt/detalhe/{something}/{id}
            var matches = Regex.Matches(html, @"/pt/(?:imovel|detalhe)[^\s""<>]*/(\d+)", RegexOptions.IgnoreCase);
            var seen = new HashSet<string>();
            foreach (Match m in matches)
            {
                var id = m.Groups[1].Value;
                if (seen.Add(id))
                    results.Add(($"{BaseUrl}{m.Value}", id));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Agency}: HTML discovery failed", AgencyName);
        }
        return results;
    }

    private async Task<Property?> ScrapeDetailAsync(string url, string externalId, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);

        // Try Nuxt data from detail page
        var nuxtMatch = Regex.Match(html, @"window\.__NUXT__\s*=\s*({.*?});", RegexOptions.Singleline);
        if (nuxtMatch.Success)
        {
            var props = ParseNuxtListings(nuxtMatch.Groups[1].Value);
            var first = props.FirstOrDefault();
            if (first is not null)
            {
                first.ListingUrl = url;
                return first;
            }
        }

        return new Property
        {
            ExternalId = externalId,
            SourceAgency = AgencyName,
            Title = $"Imóvel {externalId}",
            Currency = "EUR",
            Type = PropertyType.Other,
            Transaction = TransactionType.Sale,
            City = "Leiria",
            District = "Leiria",
            ListingUrl = url,
            Status = PropertyStatus.Active,
        };
    }

    // ── Helpers ───────────────────────────────────────────────────

    private static string? ExtractJsonString(string json, string pattern)
    {
        var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static decimal ParsePrice(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var cleaned = text.Replace(" ", "").Replace(".", "").Replace(",", ".").Replace("€", "");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m;
    }
}
