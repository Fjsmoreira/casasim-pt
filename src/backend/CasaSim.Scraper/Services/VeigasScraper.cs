using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for Veigas Imobiliária (veigas.eu) — Next.js based site.
/// Discover properties from /lista-propriedades, detail pages at /detalhe-propriedade/{slug}/{id}.
/// Next.js pages embed server-side data in __NEXT_DATA__ script tag.
/// </summary>
internal sealed class VeigasScraper : IPropertyScraper
{
    private const string BaseUrl = "https://www.veigas.eu";
    public string AgencyName => "Veigas";

    private readonly HttpClient _http;
    private readonly ILogger<VeigasScraper> _logger;

    public VeigasScraper(HttpClient http, ILogger<VeigasScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();
        try
        {
            // Discover from listing page
            var urls = await DiscoverPropertiesAsync(ct);
            _logger.LogInformation("{Agency}: discovered {Count} URLs", AgencyName, urls.Count);

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
        catch (Exception ex)
        {
            _logger.LogError(ex, "{Agency}: scrape failed", AgencyName);
        }
        _logger.LogInformation("{Agency}: scraped {Count} properties", AgencyName, results.Count);
        return results;
    }

    private async Task<List<(string Url, string Id)>> DiscoverPropertiesAsync(CancellationToken ct)
    {
        var results = new List<(string, string)>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var resp = await _http.GetAsync($"{BaseUrl}/lista-propriedades", ct);
            if (!resp.IsSuccessStatusCode) return results;
            var html = await resp.Content.ReadAsStringAsync(ct);

            // Extract property URLs: /detalhe-propriedade/{slug}/{id}
            var matches = Regex.Matches(html, @"/detalhe-propriedade/([^/]+)/(\d+)", RegexOptions.IgnoreCase);
            foreach (Match m in matches)
            {
                var id = m.Groups[2].Value;
                if (seen.Add(id))
                    results.Add(($"{BaseUrl}/detalhe-propriedade/{m.Groups[1].Value}/{id}", id));
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "{Agency}: listing discovery failed", AgencyName);
        }
        return results;
    }

    private async Task<Property?> ScrapeDetailAsync(string url, string externalId, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        // Try to extract __NEXT_DATA__ (server-side rendered JSON)
        try
        {
            var nextDataMatch = Regex.Match(html, @"<script[^>]*id=""__NEXT_DATA__""[^>]*>\s*({.*?})\s*</script>", RegexOptions.Singleline);
            if (nextDataMatch.Success)
            {
                // NEXT_DATA contains the full page props including property details
                // Parse what we can from the JSON without Newtonsoft
                var jsonText = nextDataMatch.Groups[1].Value;
                return ParseNextDataListing(jsonText, url, externalId);
            }
        }
        catch { }

        // Fallback: scrape from HTML
        return ParseFromHtml(html, url, externalId);
    }

    private Property? ParseNextDataListing(string json, string url, string externalId)
    {
        // Extract key fields from the Next.js data blob using regex
        var title = ExtractJsonString(json, @"title"":\s*""([^""]+)");
        var priceStr = ExtractJsonString(json, @"(?:price|preco|preço)"":\s*""?([^"",}]+)");
        var price = ParsePrice(priceStr ?? "");
        var description = ExtractJsonString(json, @"description"":\s*""([^""]+)");
        var bedroomsStr = ExtractJsonString(json, @"(?:bedrooms|quartos)"":\s*(\d+)");
        int? bedrooms = bedroomsStr is not null && int.TryParse(bedroomsStr, out var b) ? b : null;
        var areaStr = ExtractJsonString(json, @"(?:area|areaM2|area_m2)"":\s*(\d+[\.,]?\d*)");
        double? areaM2 = areaStr is not null && double.TryParse(areaStr.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out var a) ? a : null;

        var propType = PropertyType.Other;
        if (json.Contains("apartamento", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Apartment;
        else if (json.Contains("moradia", StringComparison.OrdinalIgnoreCase) || json.Contains("casa", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.House;
        else if (json.Contains("terreno", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Land;
        else if (json.Contains("loja", StringComparison.OrdinalIgnoreCase) || json.Contains("armazem", StringComparison.OrdinalIgnoreCase) || json.Contains("comercio", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Commercial;

        var transType = json.Contains("arrenda", StringComparison.OrdinalIgnoreCase) ? TransactionType.Rent : TransactionType.Sale;

        // Extract images
        var images = new List<string>();
        var imgMatches = Regex.Matches(json, @"(?:image|img|foto|photo|url|src)"":\s*""(https?://[^""]+\.(?:jpg|jpeg|png|webp)[^""]*)""", RegexOptions.IgnoreCase);
        foreach (Match m in imgMatches)
            if (!images.Contains(m.Groups[1].Value)) images.Add(m.Groups[1].Value);

        return new Property
        {
            ExternalId = externalId,
            SourceAgency = AgencyName,
            Title = title ?? $"Propriedade em Portugal",
            Description = description,
            Price = price,
            Currency = "EUR",
            Type = propType,
            Transaction = transType,
            City = "Pombal",
            District = "Leiria",
            AreaM2 = areaM2,
            Bedrooms = bedrooms,
            Images = images,
            ListingUrl = url,
            Status = PropertyStatus.Active,
        };
    }

    private Property? ParseFromHtml(string html, string url, string externalId)
    {
        var titleMatch = Regex.Match(html, @"<title>([^<]+)</title>", RegexOptions.IgnoreCase);
        var title = titleMatch.Success ? titleMatch.Groups[1].Value.Trim() : externalId;

        var price = ExtractPrice(html);
        var bedrooms = ExtractInt(html, @"(\d+)\s*quartos?");
        var areaM2 = ExtractDouble(html, @"(\d+[\.,]?\d*)\s*m²");

        var propType = PropertyType.Other;
        if (html.Contains("apartamento", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Apartment;
        else if (html.Contains("moradia", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.House;
        else if (html.Contains("terreno", StringComparison.OrdinalIgnoreCase)) propType = PropertyType.Land;

        var images = new List<string>();
        var imgMatches = Regex.Matches(html, @"https?://[^""\s]+\.(?:jpg|jpeg|png|webp)", RegexOptions.IgnoreCase);
        foreach (Match m in imgMatches)
            if (!images.Contains(m.Value)) images.Add(m.Value);

        return new Property
        {
            ExternalId = externalId,
            SourceAgency = AgencyName,
            Title = title,
            Price = price,
            Currency = "EUR",
            Type = propType,
            Transaction = TransactionType.Sale,
            City = "Pombal",
            District = "Leiria",
            AreaM2 = areaM2,
            Bedrooms = bedrooms,
            Images = images,
            ListingUrl = url,
            Status = PropertyStatus.Active,
        };
    }

    // ── Parsing helpers ──────────────────────────────────────────

    private static string? ExtractJsonString(string json, string pattern)
    {
        var match = Regex.Match(json, pattern, RegexOptions.IgnoreCase);
        return match.Success ? match.Groups[1].Value : null;
    }

    private static decimal ParsePrice(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var match = Regex.Match(text, @"(\d[\d\s\.]*)\s*€?", RegexOptions.IgnoreCase);
        if (!match.Success) return 0m;
        var cleaned = match.Groups[1].Value.Replace(" ", "").Replace(".", "");
        return decimal.TryParse(cleaned, out var p) ? p : 0m;
    }

    private static decimal ExtractPrice(string text)
    {
        var match = Regex.Match(text, @"(\d[\d\s\.]*)\s*€", RegexOptions.IgnoreCase);
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
}
