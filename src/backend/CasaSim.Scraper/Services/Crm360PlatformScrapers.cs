using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Scraper for Valorfin Imóveis (valorfinimoveis.pt) — CRM360 platform.
/// </summary>
internal sealed class ValorfinImoveisScraper : IPropertyScraper
{
    private const string BaseUrl = "https://valorfinimoveis.pt";
    public string AgencyName => "Valorfin Imóveis";

    private readonly HttpClient _http;
    private readonly ILogger<ValorfinImoveisScraper> _logger;

    public ValorfinImoveisScraper(HttpClient http, ILogger<ValorfinImoveisScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        return await Crm360Parser.ScrapeSiteAsync(BaseUrl, AgencyName, _http, _logger, ct);
    }
}

/// <summary>
/// Scraper for Argilipe Imobiliária (argilipe.pt) — CRM360 platform.
/// </summary>
internal sealed class ArgilipeScraper : IPropertyScraper
{
    private const string BaseUrl = "https://www.argilipe.pt";
    public string AgencyName => "Argilipe";

    private readonly HttpClient _http;
    private readonly ILogger<ArgilipeScraper> _logger;

    public ArgilipeScraper(HttpClient http, ILogger<ArgilipeScraper> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        return await Crm360Parser.ScrapeSiteAsync(BaseUrl, AgencyName, _http, _logger, ct);
    }
}

/// <summary>
/// Shared parsing logic for CRM360 platform sites.
/// </summary>
internal static class Crm360Parser
{
    public static async Task<IReadOnlyList<Property>> ScrapeSiteAsync(
        string baseUrl, string agencyName, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var results = new List<Property>();
        try
        {
            var urls = await DiscoverPropertiesAsync(baseUrl, agencyName, http, logger, ct);
            logger.LogInformation("{Agency}: discovered {Count} property URLs", agencyName, urls.Count);

            foreach (var (url, extId) in urls)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    var prop = await ScrapeDetailAsync(baseUrl, url, extId, agencyName, http, ct);
                    if (prop is not null) results.Add(prop);
                    await Task.Delay(800, ct);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "{Agency}: failed to scrape {Url}", agencyName, url);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{Agency}: scrape cycle failed", agencyName);
        }
        logger.LogInformation("{Agency}: scraped {Count} properties", agencyName, results.Count);
        return results;
    }

    private static async Task<List<(string Url, string ExternalId)>> DiscoverPropertiesAsync(
        string baseUrl, string agencyName, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var discovered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var results = new List<(string, string)>();
        int page = 1;

        while (page <= 50)
        {
            var pageUrl = $"{baseUrl}/Imoveis?page={page}";
            string html;
            try
            {
                var resp = await http.GetAsync(pageUrl, ct);
                if (!resp.IsSuccessStatusCode) break;
                html = await resp.Content.ReadAsStringAsync(ct);
            }
            catch { break; }

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var links = doc.DocumentNode.SelectNodes("//a[contains(@href, '/Imovel/')]");
            if (links is null) break;

            int newFound = 0;
            foreach (var link in links)
            {
                var href = link.GetAttributeValue("href", "");
                if (string.IsNullOrEmpty(href)) continue;
                var match = Regex.Match(href, @"/Imovel/([a-z0-9]+)", RegexOptions.IgnoreCase);
                if (!match.Success) continue;

                var shortcode = match.Groups[1].Value;
                if (discovered.Add(shortcode))
                {
                    results.Add(($"{baseUrl}/Imovel/{shortcode}", shortcode));
                    newFound++;
                }
            }

            if (newFound == 0) break;
            page++;
            await Task.Delay(500, ct);
        }
        return results;
    }

    private static async Task<Property?> ScrapeDetailAsync(
        string baseUrl, string url, string externalId, string agencyName, HttpClient http, CancellationToken ct)
    {
        var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // ── Price ──────────────────────────────────────────────
        var priceNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imovs_title_price')]");
        var priceText = priceNode?.InnerText.Trim() ?? "";
        var price = ParsePrice(priceText);

        // ── Transaction type ───────────────────────────────────
        var negocioNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imov_detail_negocio')]");
        var negocioText = negocioNode?.InnerText.Trim().ToLowerInvariant() ?? "";
        var transType = negocioText switch
        {
            "arrendar" => TransactionType.Rent,
            _ => TransactionType.Sale
        };

        // ── Title ──────────────────────────────────────────────
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var title = titleNode?.InnerText.Trim() ?? "";

        // ── Reference ──────────────────────────────────────────
        var refNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imovs_place_ref')]");
        var reference = refNode?.InnerText.Trim() ?? externalId;
        var refMatch = Regex.Match(reference, @"Ref\.?\s*(.+)", RegexOptions.IgnoreCase);
        reference = refMatch.Success ? refMatch.Groups[1].Value.Trim() : externalId;

        // ── Description ────────────────────────────────────────
        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']");
        var description = metaDesc?.GetAttributeValue("content", "") ?? "";

        // ── Detail fields from description ─────────────────────
        var bedrooms = ExtractInt(description, @"(\d+)\s*quartos?");
        var bathrooms = ExtractInt(description, @"(\d+)\s*(?:casas?\s*(?:de\s*)?banho|wc|w\.?c\.?)");
        var areaM2 = ExtractDoubleFromDesc(description);
        var landAreaM2 = ExtractDouble(description, @"(?:terreno|lote).*?(\d[\d\s\.,]*)\s*m²");

        // ── Property type ──────────────────────────────────────
        var propType = DetectPropertyType(title, description);

        // ── Images ─────────────────────────────────────────────
        var images = new List<string>();
        var imgNodes = doc.DocumentNode.SelectNodes("//img[contains(@src, 'crm360') or contains(@src, 'imov')]");
        if (imgNodes is not null)
        {
            foreach (var img in imgNodes)
            {
                var src = img.GetAttributeValue("src", "");
                if (!string.IsNullOrEmpty(src))
                {
                    if (!src.StartsWith("http")) src = baseUrl.TrimEnd('/') + (src.StartsWith("/") ? "" : "/") + src;
                    if (src.Contains("crm360.pt") || src.Contains("/Imovel/") || src.Contains("/imov"))
                        images.Add(src);
                }
            }
        }

        return new Property
        {
            ExternalId = reference,
            SourceAgency = agencyName,
            Title = !string.IsNullOrWhiteSpace(title) ? title : $"{propType} em Pombal",
            Description = description,
            Price = price,
            Currency = "EUR",
            Type = propType,
            Transaction = transType,
            City = "Pombal",
            District = "Leiria",
            AreaM2 = areaM2,
            LandAreaM2 = landAreaM2,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            Images = images,
            ListingUrl = url,
            Status = PropertyStatus.Active,
        };
    }

    private static decimal ParsePrice(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var cleaned = Regex.Replace(text, @"[€\sEUR]", "");
        cleaned = cleaned.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m;
    }

    private static PropertyType DetectPropertyType(string title, string description)
    {
        var combined = (title + " " + description).ToLowerInvariant();
        if (combined.Contains("apartamento") || combined.Contains("t0") || combined.Contains("t1") ||
            combined.Contains("t2") || combined.Contains("t3") || combined.Contains("t4")) return PropertyType.Apartment;
        if (combined.Contains("moradia") || combined.Contains("vivenda") || combined.Contains("casa")) return PropertyType.House;
        if (combined.Contains("terreno") || combined.Contains("lote")) return PropertyType.Land;
        if (combined.Contains("loja") || combined.Contains("armazém") || combined.Contains("armazem") ||
            combined.Contains("escritório") || combined.Contains("escritorio") || combined.Contains("prédio") ||
            combined.Contains("predio") || combined.Contains("comércio") || combined.Contains("comercio")) return PropertyType.Commercial;
        return PropertyType.Other;
    }

    private static int? ExtractInt(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (match.Success && int.TryParse(match.Groups[1].Value.Trim(), out var v)) return v;
        return null;
    }

    private static double? ExtractDouble(string text, string pattern)
    {
        var match = Regex.Match(text, pattern, RegexOptions.IgnoreCase);
        if (!match.Success) return null;
        var cleaned = Regex.Replace(match.Groups[1].Value, @"\s", "").Replace(",", ".");
        return double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : null;
    }

    private static double? ExtractDoubleFromDesc(string text)
    {
        // Try m² with preceding number
        var match = Regex.Match(text, @"(\d[\d\s\.,]*)\s*m²", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var cleaned = Regex.Replace(match.Groups[1].Value, @"\s", "").Replace(",", ".");
            if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var v)) return v;
        }
        return null;
    }
}
