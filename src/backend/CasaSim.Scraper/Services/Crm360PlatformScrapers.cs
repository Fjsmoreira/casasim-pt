using System.Globalization;
using System.Net;
using System.Text.Json;
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
                    var prop = await ScrapeDetailAsync(baseUrl, url, extId, agencyName, http, logger, ct);
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
        string baseUrl, string url, string externalId, string agencyName, HttpClient http, ILogger logger, CancellationToken ct)
    {
        var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var html = await resp.Content.ReadAsStringAsync(ct);
        if (string.IsNullOrWhiteSpace(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        // ── Price ──────────────────────────────────────────────
        var priceNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imovs_title_price')]");
        var priceText = CleanText(priceNode?.InnerText ?? "");
        var price = ParsePrice(priceText);

        // ── Transaction type ───────────────────────────────────
        var negocioNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imov_detail_negocio')]");
        var negocioText = CleanText(negocioNode?.InnerText ?? "").ToLowerInvariant();
        var transType = negocioText switch
        {
            "arrendar" => TransactionType.Rent,
            _ => TransactionType.Sale
        };

        // ── Title ──────────────────────────────────────────────
        var titleNode = doc.DocumentNode.SelectSingleNode("//title");
        var visibleTitle = CleanTitle(priceText);
        var title = !string.IsNullOrWhiteSpace(visibleTitle)
            ? visibleTitle
            : CleanText(titleNode?.InnerText ?? "");

        // ── Reference ──────────────────────────────────────────
        var refNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'imovs_place_ref') and contains(normalize-space(.), 'Ref')]");
        var reference = CleanText(refNode?.InnerText ?? externalId);
        var refMatch = Regex.Match(reference, @"Ref\.?\s*(.+)", RegexOptions.IgnoreCase);
        reference = refMatch.Success ? refMatch.Groups[1].Value.Trim() : externalId;

        // ── Description ────────────────────────────────────────
        var description = ExtractDescription(doc);

        // ── Detail fields from description ─────────────────────
        var bedrooms = ExtractInt(description, @"(\d+)\s*quartos?");
        var bathrooms = ExtractInt(description, @"(\d+)\s*(?:casas?\s*(?:de\s*)?banho|wc|w\.?c\.?)");
        var areaM2 = ExtractDoubleFromDesc(description);
        var landAreaM2 = ExtractDouble(description, @"(?:terreno|lote).*?(\d[\d\s\.,]*)\s*m²");
        var yearBuilt = (int?)null;
        var detailText = CleanText(doc.DocumentNode.InnerText);
        bedrooms ??= ExtractInt(title, @"\bT(\d+)\b");
        bedrooms ??= ExtractInt(detailText, @"(\d+)\s*Quartos?");
        bathrooms ??= ExtractInt(detailText, @"(\d+)\s*WC");

        // ── Location from page ──────────────────────────────────
        // CRM360 pages have structured location data in .another_details divs:
        //   <div class="another_details">Distrito: Leiria</div>
        //   <div class="another_details">Concelho: Pombal</div>
        //   <div class="another_details">Freguesia: Vermoil</div>
        var city = "Pombal";
        var district = "Leiria";
        var anotherDetailNodes = doc.DocumentNode.SelectNodes("//div[contains(@class, 'another_details')]");
        if (anotherDetailNodes is not null)
        {
            foreach (var detail in anotherDetailNodes)
            {
                var text = CleanText(detail.InnerText);
                if (text.StartsWith("Concelho:", StringComparison.OrdinalIgnoreCase))
                    city = text["Concelho:".Length..].Trim();
                else if (text.StartsWith("Distrito:", StringComparison.OrdinalIgnoreCase))
                    district = text["Distrito:".Length..].Trim();
                else if (text.StartsWith("Área do terreno:", StringComparison.OrdinalIgnoreCase))
                    landAreaM2 ??= ExtractDouble(text, @"Área do terreno:\s*(\d[\d\s\.,]*)\s*m²");
                else if (text.StartsWith("Área bruta:", StringComparison.OrdinalIgnoreCase))
                    areaM2 ??= ExtractDouble(text, @"Área bruta:\s*(\d[\d\s\.,]*)\s*m²");
                else if (text.StartsWith("Ano de construção:", StringComparison.OrdinalIgnoreCase))
                    yearBuilt ??= ExtractInt(text, @"Ano de construção:\s*(\d{4})");
            }
        }
        // Fallback: try meta keywords (format: "Moradia, Usado, Leiria, Pombal")
        if (city == "Pombal" && district == "Leiria")
        {
            var metaKeywords = doc.DocumentNode.SelectSingleNode("//meta[@name='keywords']");
            var kwContent = metaKeywords?.GetAttributeValue("content", "") ?? "";
            var kwParts = kwContent.Split(',').Select(p => p.Trim()).ToArray();
            if (kwParts.Length >= 4)
            {
                district = kwParts[^2];
                city = kwParts[^1];
            }
        }
        // ── Property type ──────────────────────────────────────
        var propType = DetectPropertyType(title, description);

        var images = await ExtractImagesAsync(baseUrl, externalId, doc, http, logger, ct);

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
            City = city,
            District = district,
            AreaM2 = areaM2,
            LandAreaM2 = landAreaM2,
            Bedrooms = bedrooms,
            Bathrooms = bathrooms,
            YearBuilt = yearBuilt,
            Images = images,
            ListingUrl = url,
            Status = DetectStatus(doc),
        };
    }

    private static decimal ParsePrice(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0m;
        var priceMatches = Regex.Matches(
            text,
            @"(?<![A-Za-z])(\d{1,3}(?:[ .]\d{3})*(?:,\d{2})?|\d+(?:,\d{2})?)\s*(?:€|EUR)",
            RegexOptions.IgnoreCase);
        if (priceMatches.Count == 0) return 0m;

        var cleaned = Regex.Replace(priceMatches[^1].Groups[1].Value, @"\s", "");
        cleaned = cleaned.Replace(".", "").Replace(",", ".");
        return decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var p) ? p : 0m;
    }

    private static string ExtractDescription(HtmlDocument doc)
    {
        var descriptionNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'detail_imovs_description')]");
        if (descriptionNode is not null)
        {
            var visibleDescription = CleanHtmlText(descriptionNode.InnerHtml);
            if (!string.IsNullOrWhiteSpace(visibleDescription))
            {
                return visibleDescription;
            }
        }

        var metaDesc = doc.DocumentNode.SelectSingleNode("//meta[@name='description']")
            ?? doc.DocumentNode.SelectSingleNode("//meta[@property='og:description']");
        return CleanHtmlText(metaDesc?.GetAttributeValue("content", "") ?? "");
    }

    private static async Task<List<string>> ExtractImagesAsync(
        string baseUrl,
        string externalId,
        HtmlDocument doc,
        HttpClient http,
        ILogger logger,
        CancellationToken ct)
    {
        var images = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var previewUrl = new Uri(new Uri(baseUrl), $"/imovel/preview/images?id={Uri.EscapeDataString(externalId)}");
        try
        {
            using var response = await http.GetAsync(previewUrl, ct);
            if (response.IsSuccessStatusCode)
            {
                await using var stream = await response.Content.ReadAsStreamAsync(ct);
                using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
                if (json.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in json.RootElement.EnumerateArray())
                    {
                        AddImage(item.GetString(), baseUrl, externalId, images, seen);
                    }
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException)
        {
            logger.LogDebug(ex, "CRM360 image preview endpoint failed for {ExternalId}", externalId);
        }

        if (images.Count > 0)
        {
            return images;
        }

        var candidates = doc.DocumentNode
            .SelectNodes("//*[@src or @data-src or @data-original or @href or @content]")
            ?.SelectMany(node => new[]
            {
                node.GetAttributeValue("src", ""),
                node.GetAttributeValue("data-src", ""),
                node.GetAttributeValue("data-original", ""),
                node.GetAttributeValue("href", ""),
                node.GetAttributeValue("content", ""),
            }) ?? [];

        foreach (var candidate in candidates)
        {
            AddImage(candidate, baseUrl, externalId, images, seen);
        }

        foreach (Match match in Regex.Matches(
            doc.DocumentNode.OuterHtml,
            $@"https?://images\.crm360\.pt/imoveis/{Regex.Escape(externalId)}/[^'""<>)\s]+?\.(?:jpe?g|png|webp)",
            RegexOptions.IgnoreCase))
        {
            AddImage(match.Value, baseUrl, externalId, images, seen);
        }

        return images;
    }

    private static void AddImage(
        string? rawUrl,
        string baseUrl,
        string externalId,
        List<string> images,
        HashSet<string> seen)
    {
        if (string.IsNullOrWhiteSpace(rawUrl)) return;

        var url = WebUtility.HtmlDecode(rawUrl).Trim();
        if (url.StartsWith("//", StringComparison.Ordinal))
        {
            url = "https:" + url;
        }
        else if (url.StartsWith("/", StringComparison.Ordinal))
        {
            url = new Uri(new Uri(baseUrl), url).ToString();
        }

        if (!url.Contains("images.crm360.pt/imoveis/", StringComparison.OrdinalIgnoreCase) ||
            !url.Contains($"/{externalId}/", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        url = Regex.Replace(url, @"\?.*$", "");
        url = url.Replace("/tn/", "/foto_marca_agua/", StringComparison.OrdinalIgnoreCase);

        if (seen.Add(url))
        {
            images.Add(url);
        }
    }

    private static PropertyStatus DetectStatus(HtmlDocument doc)
    {
        var statusText = CleanText(string.Join(" ", doc.DocumentNode
            .SelectNodes("//*[contains(@class, 'imovs_negocio')]")
            ?.Select(node => node.InnerText) ?? []));

        if (Regex.IsMatch(statusText, @"\bVendido\b", RegexOptions.IgnoreCase))
        {
            return PropertyStatus.Sold;
        }

        if (Regex.IsMatch(statusText, @"\bArrendad[ao]\b", RegexOptions.IgnoreCase))
        {
            return PropertyStatus.Rented;
        }

        return PropertyStatus.Active;
    }

    private static string CleanTitle(string text)
    {
        var title = Regex.Replace(text, @"\d[\d\s\.,]*\s*(?:€|EUR)", "", RegexOptions.IgnoreCase);
        return CleanText(title);
    }

    private static string CleanHtmlText(string html)
    {
        if (string.IsNullOrWhiteSpace(html)) return "";

        var withBreaks = Regex.Replace(html, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        withBreaks = Regex.Replace(withBreaks, @"</\s*(p|div|li|ul|ol|h[1-6])\s*>", "\n", RegexOptions.IgnoreCase);

        var doc = new HtmlDocument();
        doc.LoadHtml(withBreaks);
        return CleanText(doc.DocumentNode.InnerText);
    }

    private static string CleanText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";

        var decoded = WebUtility.HtmlDecode(text).Replace('\u00a0', ' ');
        decoded = Regex.Replace(decoded, @"[ \t\r\f\v]+", " ");
        decoded = Regex.Replace(decoded, @"\n\s+", "\n");
        decoded = Regex.Replace(decoded, @"\n{3,}", "\n\n");
        return decoded.Trim();
    }

    private static PropertyType DetectPropertyType(string title, string description)
    {
        var combined = (title + " " + description).ToLowerInvariant();
        if (combined.Contains("terreno") || combined.Contains("lote")) return PropertyType.Land;
        if (combined.Contains("moradia") || combined.Contains("vivenda") || combined.Contains("casa")) return PropertyType.House;
        if (combined.Contains("apartamento") || combined.Contains("t0") || combined.Contains("t1") ||
            combined.Contains("t2") || combined.Contains("t3") || combined.Contains("t4")) return PropertyType.Apartment;
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
