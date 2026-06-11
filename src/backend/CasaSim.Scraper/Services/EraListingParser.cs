using System.Globalization;
using System.Text.RegularExpressions;
using CasaSim.Core.Models;
using HtmlAgilityPack;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Parses ERA Portugal detail-page HTML into normalized <see cref="ParsedListing"/> instances.
///
/// ERA's detail pages are rendered client-side by React (in a DNN/DotNetNuke ASP.NET SPA).
/// After the React app hydrates the page, the DOM contains the full listing data structured
/// as:
///
///   <div class="property-reference">Ref.: {externalId}</div>
///   <div class="property-place">{PropertyTypeLabel}, {CITY}, {Parish}</div>
///   <div class="property-details">
///     <div class="detail">
///       <span class="lbl">{key}:</span><span class="value">{value}</span>
///     </div>
///     ...
///   </div>
///   <div class="price">
///     <span class="price-type">{Comprar|Arrendar}</span>
///     <span class="price-value">{amount} €</span>
///   </div>
///
/// Detail keys used: Quartos, Casas de Banho, Área Útil (m2), Área Bruta Privativa (m2),
/// Área Terreno (m2), Estacionamento, Piso, Certificado Energético.
/// </summary>
public sealed class EraListingParser
{
    private const string AgencyName = "ERA";
    private const string AgencySlug = "era-pombal";

    /// <summary>
    /// Parse a full ERA detail page HTML into a <see cref="ParsedListing"/>.
    /// </summary>
    /// <param name="html">Full HTML of the ERA detail page (after React hydration).</param>
    /// <param name="sourceUrl">Optional source URL of the listing page.</param>
    /// <returns>A populated ParsedListing, or null if critical fields are missing.</returns>
    public ParsedListing? ParseFromHtml(string html, string? sourceUrl = null)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        return MapToParsedListing(doc, sourceUrl);
    }

    // ── Core mapping ─────────────────────────────────────────────

    private static ParsedListing? MapToParsedListing(HtmlDocument doc, string? sourceUrl)
    {
        try
        {
            var reference = ExtractReference(doc);
            if (string.IsNullOrEmpty(reference))
                return null;

            var location = ExtractLocation(doc);
            var details = ExtractDetails(doc);
            var (transactionType, price) = ExtractPrice(doc);

            return new ParsedListing
            {
                ExternalId = reference,
                Title = BuildTitle(location.typeLabel, details, location.parish, location.city),
                Price = price,
                Currency = "EUR",
                Type = MapPropertyType(location.typeLabel),
                Transaction = transactionType,
                Address = null,
                City = location.city,
                District = "Leiria",
                Parish = location.parish,
                PostalCode = null,
                Latitude = null,
                Longitude = null,
                AreaM2 = details.AreaM2,
                LandAreaM2 = details.LandAreaM2,
                Bedrooms = details.Bedrooms,
                Bathrooms = details.Bathrooms,
                ParkingSpots = details.ParkingSpots,
                YearBuilt = null,
                EnergyClass = details.EnergyClass,
                Images = ExtractImages(doc),
                ListingUrl = sourceUrl,
                Status = PropertyStatus.Active,
            };
        }
        catch
        {
            return null;
        }
    }

    // ── Field extractors ─────────────────────────────────────────

    /// <summary>
    /// Extract the property reference from <c>.property-reference</c> text.
    /// Format: "Ref.: 404260053"
    /// </summary>
    private static string? ExtractReference(HtmlDocument doc)
    {
        var refNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'property-reference')]");
        if (refNode is null)
            return null;

        var text = refNode.InnerText.Trim();
        // Match "Ref.: 404260053" or "Ref.:404260053"
        var match = Regex.Match(text, @"Ref\.?\s*:\s*(\d+)");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract the location text from <c>.property-place</c>.
    /// Format: "Apartamento, POMBAL, Pombal" or "Moradia, ABIUL, Pombal"
    ///
    /// The parts are: {Type}, {UpperArea}, {CityOrParish}
    /// - When 3 parts: parts[2] is always the city/municipality (in proper case).
    ///   If parts[1] equals parts[2] uppercased (e.g. "POMBAL" == "Pombal".ToUpper()),
    ///   then the property is within the city itself and there's no separate parish.
    ///   Otherwise parts[1] is the parish name in uppercase.
    /// - When 2 parts: parts[1] is the combined city/parish (uppercase variant).
    /// </summary>
    private static (string typeLabel, string city, string parish) ExtractLocation(HtmlDocument doc)
    {
        var placeNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'property-place')]");
        if (placeNode is null)
            return ("Imóvel", "Pombal", string.Empty);

        var text = placeNode.InnerText.Trim();
        var parts = text.Split(',').Select(p => p.Trim()).ToArray();

        var typeLabel = parts.Length > 0 ? parts[0] : "Imóvel";

        if (parts.Length >= 3)
        {
            // 3 parts: {Type}, {UpperArea}, {CityOrParish}
            var upperArea = parts[1];
            var lastPart = parts[2];

            // If the upper area is an uppercase variant of the last part, it's the same place
            if (string.Equals(upperArea, lastPart, StringComparison.OrdinalIgnoreCase))
            {
                // "POMBAL, Pombal" → city=Pombal, no separate parish
                return (typeLabel, ProperCase(lastPart), string.Empty);
            }

            // Otherwise upper area is the parish, last part is the city
            // "ABIUL, Pombal" → city=Pombal, parish=Abiul
            return (typeLabel, ProperCase(lastPart), ProperCase(upperArea));
        }

        if (parts.Length >= 2)
        {
            // 2 parts: {Type}, {CityOrParish}
            return (typeLabel, ProperCase(parts[1]), string.Empty);
        }

        return (typeLabel, "Pombal", string.Empty);
    }

    /// <summary>
    /// Convert a string to proper case (first letter uppercase, rest lowercase).
    /// Handles "POMBAL" → "Pombal", "Pombal" → "Pombal".
    /// </summary>
    private static string ProperCase(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        // If it's all uppercase, convert to proper case
        if (value.All(c => !char.IsLetter(c) || char.IsUpper(c)))
        {
            return char.ToUpperInvariant(value[0]) + value[1..].ToLowerInvariant();
        }

        return value;
    }

    /// <summary>
    /// Extract structured details from <c>.property-details .detail</c> elements.
    /// Each detail has: <span class="lbl">{key}:</span><span class="value">{value}</span>
    /// Known keys: Quartos, Casas de Banho, Área Útil (m2), Área Bruta Privativa (m2),
    /// Área Terreno (m2), Estacionamento, Piso, Certificado Energético, Preço m2/…
    /// </summary>
    private static ExtractedDetails ExtractDetails(HtmlDocument doc)
    {
        var details = new ExtractedDetails();

        var detailNodes = doc.DocumentNode.SelectNodes("//*[contains(@class, 'property-details')]//*[contains(@class, 'detail')]");
        if (detailNodes is null)
            return details;

        foreach (var node in detailNodes)
        {
            var lblNode = node.SelectSingleNode(".//*[contains(@class, 'lbl')]");
            var valNode = node.SelectSingleNode(".//*[contains(@class, 'value')]");

            if (lblNode is null || valNode is null)
                continue;

            var key = lblNode.InnerText.Trim().TrimEnd(':');
            var rawValue = valNode.InnerText.Trim();

            switch (key)
            {
                case "Quartos":
                    details.Bedrooms = ParseInt(rawValue);
                    break;
                case "Casas de Banho":
                    details.Bathrooms = ParseInt(rawValue);
                    break;
                case "Área Útil (m2)":
                case "Área útil (m2)":
                    details.AreaM2 = ParseDouble(rawValue);
                    break;
                case "Área Bruta Privativa (m2)":
                case "Área bruta privativa (m2)":
                    // Use this as a fallback for AreaM2 if Área Útil is not available
                    if (!details.AreaM2.HasValue || details.AreaM2 == 0)
                        details.AreaM2 = ParseDouble(rawValue);
                    break;
                case "Área Terreno (m2)":
                case "Área do terreno (m2)":
                    details.LandAreaM2 = ParseDouble(rawValue);
                    break;
                case "Estacionamento":
                    details.ParkingSpots = ParseInt(rawValue);
                    break;
                case "Piso":
                    // Floor number — stored but not in ParsedListing directly
                    break;
                case "Certificado Energético":
                case "Certificado energético":
                    details.EnergyClass = rawValue;
                    break;
            }
        }

        return details;
    }

    /// <summary>
    /// Extract the transaction type and price from the price section.
    /// Structure: <span class="price-type">Comprar </span><span class="price-value">258.000 €</span>
    /// For "Sob Consulta" (upon request), price defaults to 0.
    /// </summary>
    private static (TransactionType transaction, decimal price) ExtractPrice(HtmlDocument doc)
    {
        var priceTypeNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'price-type')]");
        var priceValueNode = doc.DocumentNode.SelectSingleNode("//*[contains(@class, 'price-value')]");

        var transaction = TransactionType.Sale;
        var price = 0m;

        if (priceTypeNode is not null)
        {
            var typeText = priceTypeNode.InnerText.Trim().ToLowerInvariant();
            transaction = typeText switch
            {
                "arrendar" => TransactionType.Rent,
                "comprar" => TransactionType.Sale,
                _ => TransactionType.Sale,
            };
        }

        if (priceValueNode is not null)
        {
            var rawValue = priceValueNode.InnerText.Trim();

            // Handle "Sob Consulta" (upon request) — no price
            if (rawValue.Equals("Sob Consulta", StringComparison.OrdinalIgnoreCase) ||
                rawValue.Equals("Sob consulta", StringComparison.OrdinalIgnoreCase))
            {
                price = 0m;
            }
            else
            {
                // Parse price like "258.000 €" or "1.250 €"
                // Remove the currency symbol and any non-numeric chars (except dots and commas)
                price = ParsePrice(rawValue);
            }
        }

        return (transaction, price);
    }

    /// <summary>
    /// Extract image URLs from img tags with src containing era.pt media paths.
    /// </summary>
    private static List<string> ExtractImages(HtmlDocument doc)
    {
        var imageNodes = doc.DocumentNode.SelectNodes("//img[contains(@src, 'media') or contains(@src, 'ImageResize')]");
        if (imageNodes is null)
            return [];

        var urls = new List<string>();
        foreach (var img in imageNodes)
        {
            var src = img.GetAttributeValue("src", null);
            if (!string.IsNullOrEmpty(src) && !urls.Contains(src))
                urls.Add(src);
        }

        return urls;
    }

    // ── Type mapping ─────────────────────────────────────────────

    private static PropertyType MapPropertyType(string typeLabel)
    {
        var lower = typeLabel.ToLowerInvariant().Trim();
        return lower switch
        {
            "apartamento" => PropertyType.Apartment,
            "moradia" => PropertyType.House,
            "moradia geminada" => PropertyType.Townhouse,
            "moradia band" => PropertyType.House,
            "vivenda" => PropertyType.Villa,
            "villa" => PropertyType.Villa,
            "terreno" => PropertyType.Land,
            "terreno urbano" => PropertyType.Land,
            "loja" => PropertyType.Commercial,
            "armazém" or "armazem" => PropertyType.Commercial,
            "escritório" or "escritorio" => PropertyType.Commercial,
            "prédio" or "predio" => PropertyType.Commercial,
            "garagem" => PropertyType.Other,
            _ => PropertyType.Other,
        };
    }

    // ── Title builder ────────────────────────────────────────────

    private static string BuildTitle(string typeLabel, ExtractedDetails details, string parish, string city)
    {
        var typeName = PortuguesePropertyTypeName(typeLabel);
        var bedPart = details.Bedrooms.HasValue ? $" T{details.Bedrooms}" : string.Empty;

        var locationParts = new[] { parish, city }.Where(p => !string.IsNullOrEmpty(p));
        var location = string.Join(", ", locationParts);
        var locationPart = !string.IsNullOrEmpty(location) ? $" em {location}" : string.Empty;

        return $"{typeName}{bedPart} à venda{locationPart}".Trim();
    }

    private static string PortuguesePropertyTypeName(string typeLabel)
    {
        var lower = typeLabel.ToLowerInvariant().Trim();
        return lower switch
        {
            "apartamento" => "Apartamento",
            "moradia" => "Moradia",
            "moradia geminada" => "Moradia Geminada",
            "vivenda" => "Vivenda",
            "terreno" => "Terreno",
            "loja" => "Loja",
            "armazém" or "armazem" => "Armazém",
            "escritório" or "escritorio" => "Escritório",
            "prédio" or "predio" => "Prédio",
            "garagem" => "Garagem",
            _ => typeLabel,
        };
    }

    // ── Parsing helpers ──────────────────────────────────────────

    private static int? ParseInt(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Remove non-numeric chars (keep digits)
        var cleaned = Regex.Replace(value, @"[^\d]", "");
        if (int.TryParse(cleaned, out var result))
            return result;

        return null;
    }

    private static double? ParseDouble(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Portuguese format: "101" or "1 000" — remove spaces, parse invariant
        var cleaned = value.Replace(" ", "").Trim();
        if (double.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return null;
    }

    private static decimal ParsePrice(string value)
    {
        if (string.IsNullOrEmpty(value))
            return 0m;

        // ERA prices: "258.000 €" — the dot is thousands separator, comma is decimal
        // First strip currency symbols and whitespace
        var cleaned = value.Replace("€", "").Replace("EUR", "").Trim();

        // Portuguese format: 258.000 € → 258000
        // Remove dots (thousands separators), then replace comma with dot
        cleaned = cleaned.Replace(".", "").Replace(",", ".");

        if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            return result;

        return 0m;
    }

    // ── Internal data holder ─────────────────────────────────────

    private sealed class ExtractedDetails
    {
        public double? AreaM2 { get; set; }
        public double? LandAreaM2 { get; set; }
        public int? Bedrooms { get; set; }
        public int? Bathrooms { get; set; }
        public int? ParkingSpots { get; set; }
        public string? EnergyClass { get; set; }
    }
}
