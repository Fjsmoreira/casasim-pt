using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CasaSim.Core.Models;
using HtmlAgilityPack;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Parses Remax PT listing detail pages (Next.js SSR HTML with __NEXT_DATA__)
/// into the normalized CasaSim Property model.
///
/// Data flow through the HTML:
///   <script id="__NEXT_DATA__" type="application/json"> → props.pageProps.listingEncoded (base64)
///   → base64 decode → full listing JSON
///
/// The listing JSON can also be obtained directly from the Remax API:
///   GET https://api-v2-prod-remaxpt.devscope.net/api/Listing/GetListingByTitle?listingPublicId={id}
///
/// Image base URL: https://i.maxwork.pt/ds-l/
/// </summary>
internal sealed class RemaxListingParser
{
    private static readonly Regex NextDataRegex = new(
        @"<script\s+id=""__NEXT_DATA__""[^>]*type=""application/json"">(.*?)</script>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private const string ImageBaseUrl = "https://i.maxwork.pt/ds-l/";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parse a single Remax detail page HTML into a Property.
    /// </summary>
    /// <param name="html">Full HTML of the Next.js SSR listing page.</param>
    /// <param name="sourceUrl">Optional source URL of the listing page.
    /// If provided, the external ID is extracted from the URL path.
    /// If null, the listingTitle from the decoded JSON is used.</param>
    /// <returns>A populated Property, or null if parsing failed.</returns>
    public Property? ParseFromHtml(string html, string? sourceUrl = null)
    {
        var nextDataNullable = ExtractNextData(html);
        if (nextDataNullable is null)
            return null;

        var nextData = nextDataNullable.Value;

        // listingEncoded is base64-encoded JSON of the full listing data
        var listingEncoded = nextData
            .GetProperty("props")
            .GetProperty("pageProps")
            .GetProperty("listingEncoded")
            .GetString();

        if (listingEncoded is null)
            return null;

        byte[] decodedBytes;
        try
        {
            decodedBytes = Convert.FromBase64String(listingEncoded);
        }
        catch (FormatException)
        {
            return null;
        }

        var listingJson = Encoding.UTF8.GetString(decodedBytes);
        using var listingDoc = JsonDocument.Parse(listingJson);
        var listing = listingDoc.RootElement;

        return MapToProperty(listing, sourceUrl);
    }

    /// <summary>
    /// Parse a raw listing JSON (from the Remax API or decoded from listingEncoded)
    /// into a Property.
    /// </summary>
    public Property? ParseFromJson(string json, string? sourceUrl = null)
    {
        using var doc = JsonDocument.Parse(json);
        return MapToProperty(doc.RootElement, sourceUrl);
    }

    private static JsonElement? ExtractNextData(string html)
    {
        var match = NextDataRegex.Match(html);
        if (!match.Success)
            return null;

        // Also try HtmlAgilityPack for robust extraction
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var scriptNode = doc.DocumentNode.SelectSingleNode("//script[@id='__NEXT_DATA__']");
        if (scriptNode is null)
            return null;

        var json = scriptNode.InnerHtml;
        using var nextData = JsonDocument.Parse(json);
        return nextData.RootElement.Clone();
    }

    private Property MapToListingData(JsonElement element)
    {
        // Fallback: parse the full listing JSON directly for fields
        return new Property();
    }

    private Property MapToProperty(JsonElement listing, string? sourceUrl)
    {
        var prop = new Property
        {
            SourceAgency = "Remax",
            Currency = "EUR",
        };

        // --- External ID ---
        // From URL slug last segment (e.g., "122591135-5"), or listingTitle
        if (sourceUrl is not null)
        {
            var slug = sourceUrl.TrimEnd('/').Split('/').LastOrDefault();
            if (!string.IsNullOrEmpty(slug))
                prop.ExternalId = slug;
        }

        if (string.IsNullOrEmpty(prop.ExternalId))
        {
            prop.ExternalId = GetString(listing, "listingTitle")
                ?? GetString(listing, "id")
                ?? string.Empty;
        }

        // --- Title ---
        // Build from type + bedrooms + location for a good human-readable title
        prop.Title = BuildTitle(listing);

        // --- Description (PT) ---
        if (listing.TryGetProperty("descriptions", out var descs) && descs.ValueKind == JsonValueKind.Array)
        {
            foreach (var desc in descs.EnumerateArray())
            {
                var lang = GetString(desc, "languageCode");
                if (lang == "PT" || lang == "pt")
                {
                    var raw = GetString(desc, "description") ?? string.Empty;
                    prop.Description = StripHtml(raw);
                    break;
                }
            }
        }

        // --- Price ---
        prop.Price = GetDecimal(listing, "listingPrice") ?? 0m;

        // --- Transaction type (businessType) ---
        var businessType = GetString(listing, "businessType");
        prop.Transaction = businessType switch
        {
            "Venda" or "Venda" => TransactionType.Sale,
            "Arrendamento" or "Lease" or "Rent" => TransactionType.Rent,
            _ => TransactionType.Sale,
        };

        // --- Property type ---
        var listingType = GetString(listing, "listingType");
        prop.Type = listingType switch
        {
            "Moradia" or "House" => PropertyType.House,
            "Apartamento" or "Apartment" => PropertyType.Apartment,
            "Terreno" or "Land" => PropertyType.Land,
            "Villa" => PropertyType.Villa,
            "Moradia Geminada" or "Townhouse" => PropertyType.Townhouse,
            "Comercial" or "Loja" or "Commerce" => PropertyType.Commercial,
            _ => PropertyType.Other,
        };

        // --- Area ---
        prop.AreaM2 = GetDouble(listing, "totalArea")
            ?? GetDoubleFromDisplay(listing, "totalAreaDisplay");

        // --- Land area ---
        prop.LandAreaM2 = GetDouble(listing, "lotSize")
            ?? GetDoubleFromDisplay(listing, "lotSizeDisplay");

        // --- Bedrooms ---
        prop.Bedrooms = GetInt(listing, "numberOfBedrooms");

        // --- Bathrooms ---
        prop.Bathrooms = GetInt(listing, "numberOfWC")
            ?? GetInt(listing, "numberOfBathrooms");

        // --- Parking ---
        prop.ParkingSpots = GetInt(listing, "garageSpots");

        // --- Year built ---
        prop.YearBuilt = GetInt(listing, "constructionYear");

        // --- Energy class ---
        prop.EnergyClass = ResolveEnergyClass(GetInt(listing, "energyEfficiencyLevelID"));

        // --- Address ---
        var street = GetString(listing, "address") ?? string.Empty;
        var doorNumber = GetString(listing, "doorNumber");
        var addressParts = new List<string> { street };
        if (!string.IsNullOrEmpty(doorNumber))
        {
            // doorNumber may already include "Nº" prefix or be plain
            var suffix = doorNumber.StartsWith("Nº", StringComparison.OrdinalIgnoreCase)
                ? doorNumber
                : $"Nº{doorNumber}";
            addressParts.Add(suffix);
        }
        prop.Address = string.Join(", ", addressParts.Where(p => !string.IsNullOrEmpty(p)));

        // --- City / Parish ---
        // regionName2 = municipality (e.g., "Pombal")
        // regionName3 = parish (e.g., "Abiul")
        var parish = GetString(listing, "regionName3");
        var city = GetString(listing, "regionName2");
        var district = GetString(listing, "regionName1");

        // localZone can also be the parish name
        if (string.IsNullOrEmpty(parish))
            parish = GetString(listing, "localZone");

        prop.City = city ?? "Pombal";
        prop.District = district ?? "Leiria";

        // --- Postal code ---
        prop.PostalCode = GetString(listing, "zipCode");

        // --- Location (coordinates) ---
        var lat = GetDouble(listing, "latitude");
        var lng = GetDouble(listing, "longitude");
        if (lat.HasValue && lng.HasValue)
        {
            prop.Location = new NetTopologySuite.Geometries.Point(lng.Value, lat.Value)
            {
                SRID = 4326,
            };
        }

        // --- Images ---
        if (listing.TryGetProperty("listingPictures", out var pictures) && pictures.ValueKind == JsonValueKind.Array)
        {
            foreach (var pic in pictures.EnumerateArray())
            {
                // Pictures can be strings (URLs) or objects with a "url" property
                string? url = null;
                if (pic.ValueKind == JsonValueKind.String)
                    url = pic.GetString();
                else if (pic.TryGetProperty("url", out var urlProp) && urlProp.ValueKind == JsonValueKind.String)
                    url = urlProp.GetString();

                if (url is not null)
                {
                    // Some URLs are relative (e.g., "listings/..."), others are absolute
                    if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                        url = ImageBaseUrl + url.TrimStart('/');
                    prop.Images.Add(url);
                }
            }
        }

        // Also add the main picture if available and not already in images
        var mainPic = GetString(listing, "listingPictureUrl");
        if (mainPic is not null)
        {
            if (!mainPic.StartsWith("http://") && !mainPic.StartsWith("https://"))
                mainPic = ImageBaseUrl + mainPic.TrimStart('/');
            if (!prop.Images.Contains(mainPic))
                prop.Images.Insert(0, mainPic);
        }

        // --- Listing URL ---
        prop.ListingUrl = sourceUrl;

        return prop;
    }

    private string BuildTitle(JsonElement listing)
    {
        var typeLabel = GetString(listing, "listingType") ?? "Imóvel";
        var bedrooms = GetInt(listing, "numberOfBedrooms");
        var transactionType = GetString(listing, "businessType") ?? "Venda";
        var parish = GetString(listing, "regionName3")
            ?? GetString(listing, "localZone")
            ?? string.Empty;
        var city = GetString(listing, "regionName2") ?? string.Empty;

        var typePart = PortuguesePropertyTypeName(typeLabel);
        var bedPart = bedrooms.HasValue ? $" T{bedrooms}" : string.Empty;
        var transPart = transactionType == "Arrendamento" ? "para arrendar" : "à venda";
        var locationParts = new[] { parish, city }.Where(p => !string.IsNullOrEmpty(p));

        var location = string.Join(", ", locationParts);
        var locationPart = !string.IsNullOrEmpty(location) ? $" em {location}" : string.Empty;

        return $"{typePart}{bedPart} {transPart}{locationPart}".Trim();
    }

    private static string PortuguesePropertyTypeName(string listingType) => listingType switch
    {
        "Moradia" => "Moradia",
        "Apartamento" => "Apartamento",
        "Terreno" => "Terreno",
        "Loja" => "Loja",
        "Prédio" or "Building" => "Prédio",
        "Quinta" or "Farm" => "Quinta",
        "Garagem" => "Garagem",
        "Armazém" or "Warehouse" => "Armazém",
        "Escritório" or "Office" => "Escritório",
        "Trespasse" or "Sale of business" => "Trespasse",
        _ => listingType,
    };

    private static string? ResolveEnergyClass(int? id) => id switch
    {
        1 => "A",
        2 => "B",
        3 => "C",
        4 => "D",
        5 => "E",
        6 => "F",
        7 => "G",
        8 => "A+",
        9 => "H",
        10 => "I",
        11 => "NC",
        12 => "B-",
        13 => "Sem certificado",
        14 => "Não aplicável",
        _ => null,
    };

    private static string? GetString(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
            return val.GetString();
        return null;
    }

    private static int? GetInt(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number)
                return val.GetInt32();
            if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var i))
                return i;
        }
        return null;
    }

    private static double? GetDouble(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number)
                return val.GetDouble();
            if (val.ValueKind == JsonValueKind.String && double.TryParse(
                    val.GetString()?.Replace(" ", ""),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var d))
                return d;
        }
        return null;
    }

    private static decimal? GetDecimal(JsonElement el, string property)
    {
        if (el.TryGetProperty(property, out var val))
        {
            if (val.ValueKind == JsonValueKind.Number)
                return val.GetDecimal();
            if (val.ValueKind == JsonValueKind.String && decimal.TryParse(
                    val.GetString()?.Replace(" ", ""),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var d))
                return d;
        }
        return null;
    }

    private static double? GetDoubleFromDisplay(JsonElement el, string property)
    {
        // Display fields like "totalAreaDisplay" are strings like "78" or "1 290"
        if (el.TryGetProperty(property, out var val) && val.ValueKind == JsonValueKind.String)
        {
            var s = val.GetString()?.Replace(" ", "").Trim();
            if (!string.IsNullOrEmpty(s) && double.TryParse(s, NumberStyles.Any,
                    CultureInfo.InvariantCulture, out var d))
                return d;
        }
        return null;
    }

    private static string StripHtml(string html)
    {
        if (string.IsNullOrEmpty(html))
            return string.Empty;

        // Decode common HTML entities
        var decoded = System.Net.WebUtility.HtmlDecode(html);

        // Remove all HTML tags
        var text = Regex.Replace(decoded, "<[^>]*>", " ");

        // Collapse whitespace
        text = Regex.Replace(text, @"\s+", " ").Trim();

        return text;
    }
}
