using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for Neves &amp; Terlouw (nevesterlouw.com) — custom static site.
/// Discovery: /lista-imoveis/1 through /lista-imoveis/5 (price-ranged pages).
/// Detail: /imovel/{numericId}
/// </summary>
internal sealed class NevesterlouwScraper : IPropertyScraper
{
    private const string BaseUrl = "https://www.nevesterlouw.com";
    public string AgencyName => "Neves & Terlouw";

    private readonly HttpClient _http;
    private readonly ILogger<NevesterlouwScraper> _logger;

    public NevesterlouwScraper(HttpClient http, ILogger<NevesterlouwScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();
        try
        {
            var urls = await DiscoverPropertiesAsync(ct);
            _logger.LogInformation("{Agency}: discovered {Count} URLs", AgencyName, urls.Count);

            foreach (var (url, id) in urls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var prop = await ScrapeDetailAsync(url, id.ToString(), ct);
                    if (prop is not null) results.Add(prop);
                    await Task.Delay(500, ct);
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

    private async Task<List<(string Url, int Id)>> DiscoverPropertiesAsync(CancellationToken ct)
    {
        var discovered = new HashSet<int>();
        var results = new List<(string, int)>();

        // Known listing pages: /lista-imoveis/1 to /5 (price ranges)
        for (int page = 1; page <= 10; page++)
        {
            var pageUrl = $"{BaseUrl}/lista-imoveis/{page}";
            string html;
            try
            {
                var resp = await _http.GetAsync(pageUrl, ct);
                if (!resp.IsSuccessStatusCode) break;
                html = await resp.Content.ReadAsStringAsync(ct);
            }
            catch { break; }

            var matches = Regex.Matches(html, @"/imovel/(\d+)", RegexOptions.IgnoreCase);
            int newFound = 0;
            foreach (Match m in matches)
            {
                var id = int.Parse(m.Groups[1].Value);
                if (discovered.Add(id))
                {
                    results.Add(($"{BaseUrl}/imovel/{id}", id));
                    newFound++;
                }
            }

            if (newFound == 0) break;
            await Task.Delay(400, ct);
        }
        return results;
    }

    private async Task<Property?> ScrapeDetailAsync(string url, string externalId, CancellationToken ct)
    {
        var resp = await _http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // ── Title ──────────────────────────────────────────────
        var titleNode = doc.DocumentNode.SelectSingleNode("//h1");
        var typeText = titleNode?.InnerText.Trim() ?? "Imóvel";

        // ── Reference ──────────────────────────────────────────
        // Format: "MR-1935" or similar
        var refMatch = Regex.Match(html, @"MR-\d+", RegexOptions.IgnoreCase);
        var reference = refMatch.Success ? refMatch.Value : externalId;

        // ── Full text for parsing ──────────────────────────────
        var bodyText = doc.DocumentNode.InnerText;

        // ── Price ──────────────────────────────────────────────
        var price = ExtractPrice(bodyText);
        if (price == 0m)
            price = ExtractPrice(html);

        // ── Transaction type ───────────────────────────────────
        var transType = bodyText.Contains("arrenda", StringComparison.OrdinalIgnoreCase)
            ? TransactionType.Rent : TransactionType.Sale;

        // ── Area ───────────────────────────────────────────────
        var areaM2 = ExtractDouble(bodyText, @"(\d+[\.,]?\d*)\s*m²");
        // Try "área" pattern 
        if (!areaM2.HasValue)
            areaM2 = ExtractDouble(bodyText, @"[áa]rea\s*(?:útil|total|construção|construcao)?[^.]*?(\d+[\.,]?\d*)\s*m²");

        // ── Bedrooms ───────────────────────────────────────────
        var bedrooms = ExtractInt(bodyText, @"(\d+)\s*quartos?");

        // ── Location ───────────────────────────────────────────
        var city = "Pombal";
        var cityMatch = Regex.Match(bodyText, @"\b(Pombal|Leiria|Coimbra|Ansião|Soure|Figueiró)\b", RegexOptions.IgnoreCase);
        if (cityMatch.Success)
            city = ProperCase(cityMatch.Groups[1].Value);

        // ── Property type ──────────────────────────────────────
        var propType = typeText.ToLowerInvariant() switch
        {
            "terreno" => PropertyType.Land,
            "apartamento" => PropertyType.Apartment,
            "moradia" => PropertyType.House,
            "vivenda" => PropertyType.Villa,
            "loja" or "armazém" or "armazem" or "escritório" or "escritorio" => PropertyType.Commercial,
            "casa" => PropertyType.House,
            _ => PropertyType.Other
        };

        // ── Images ─────────────────────────────────────────────
        var images = new List<string>();
        var imgNodes = doc.DocumentNode.SelectNodes("//img[contains(@class, 'imovel') or contains(@src, 'imovel') or contains(@src, 'imgs')]");
        if (imgNodes is not null)
        {
            foreach (var img in imgNodes)
            {
                var src = img.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src) && !src.Contains("favicon") && !src.Contains("logo"))
                {
                    if (!src.StartsWith("http")) src = BaseUrl + (src.StartsWith("/") ? "" : "/") + src;
                    images.Add(src);
                }
            }
        }

        return new Property
        {
            ExternalId = reference,
            SourceAgency = AgencyName,
            Title = $"{typeText} em {city}",
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

    private static string ProperCase(string value)
    {
        if (string.IsNullOrEmpty(value)) return value;
        return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
    }
}
