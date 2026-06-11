using System.Globalization;
using System.Text.Json;
using CasaSim.Core.Models;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Parses Century21 Portugal API responses (from <c>GET /api/properties</c>)
/// into normalized <see cref="ParsedListing"/> instances.
///
/// The Century21 list endpoint returns full listing data in a single call —
/// no separate detail-page fetch is required.  Each item contains price,
/// location, area, room counts, images, and GPS coordinates.
///
/// API base: https://www.century21.pt/api
/// List:     GET /api/properties?addresses=1015&amp;address_names=Pombal&amp;ad_type=sell
/// </summary>
public sealed class Century21ListingParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Parse the full <c>GET /api/properties</c> response (object with
    /// <c>data</c> array and <c>total</c>) into a list of <see cref="ParsedListing"/>.
    /// </summary>
    /// <param name="json">Raw JSON from the API response.</param>
    /// <returns>List of parsed listings, or empty if the response is malformed.</returns>
    public IReadOnlyList<ParsedListing> ParseFromApiResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Array)
            return Array.Empty<ParsedListing>();

        var results = new List<ParsedListing>(data.GetArrayLength());
        foreach (var item in data.EnumerateArray())
        {
            var listing = MapToParsedListing(item);
            if (listing is not null)
                results.Add(listing);
        }

        return results;
    }

    /// <summary>
    /// Parse a single Century21 listing JSON object into a <see cref="ParsedListing"/>.
    /// </summary>
    /// <param name="json">Raw JSON of a single listing (one element of the <c>data</c> array).</param>
    /// <returns>A populated ParsedListing, or null if parsing fails.</returns>
    public ParsedListing? ParseSingle(string json)
    {
        using var doc = JsonDocument.Parse(json);
        return MapToParsedListing(doc.RootElement);
    }

    // ── Core mapping ─────────────────────────────────────────────

    private static ParsedListing? MapToParsedListing(JsonElement item)
    {
        try
        {
            var listing = new ParsedListing
            {
                ExternalId = GetString(item, "reference") ?? string.Empty,
                Title = GetTitle(item),
                Description = BuildDescription(item),
                Price = GetDecimal(item, "price") ?? 0m,
                Currency = "EUR",
                Type = MapAssetType(GetString(item, "asset_type")),
                Transaction = MapAdType(GetString(item, "ad_type")),
                Address = GetString(item, "address"),
                City = "Pombal",
                District = "Leiria",
                PostalCode = ExtractPostalCode(GetString(item, "address")),
                Latitude = GetDouble(item, "lat"),
                Longitude = GetDouble(item, "lng"),
                AreaM2 = GetDouble(item, "useful_area") ?? GetDouble(item, "gross_area"),
                Bedrooms = GetInt(item, "number_of_rooms"),
                Bathrooms = GetInt(item, "number_of_wcs"),
                ParkingSpots = GetInt(item, "number_of_parking_spots"),
                YearBuilt = ExtractYearBuilt(item),
                EnergyClass = null,
                Images = ExtractImages(item),
                ListingUrl = GetString(item, "link"),
                Status = PropertyStatus.Active,
                DiscoveredAt = ParseEnteredMarket(GetString(item, "entered_market")),
            };

            return listing;
        }
        catch
        {
            return null;
        }
    }

    // ── Field extractors ─────────────────────────────────────────

    /// <summary>
    /// Build the title from the Portuguese-language title field.
    /// Falls back to English, then to a generated title from type/bedrooms.
    /// </summary>
    private static string GetTitle(JsonElement item)
    {
        if (item.TryGetProperty("title", out var title) && title.ValueKind == JsonValueKind.Object)
        {
            var pt = GetString(title, "pt");
            if (!string.IsNullOrEmpty(pt))
                return pt;

            var en = GetString(title, "en");
            if (!string.IsNullOrEmpty(en))
                return en;
        }

        // Fallback: build from type + rooms
        var typeLabel = PortuguesePropertyTypeName(GetString(item, "asset_type"));
        var rooms = GetInt(item, "number_of_rooms");
        var roomPart = rooms.HasValue && rooms > 0 ? $" T{rooms}" : string.Empty;
        return $"{typeLabel}{roomPart}";
    }

    /// <summary>
    /// Build a description from the characteristics list and available data.
    /// The list API doesn't return a free-text description, but the
    /// characteristics array provides structured amenity data.
    /// </summary>
    private static string? BuildDescription(JsonElement item)
    {
        if (!item.TryGetProperty("characteristics", out var chars) || chars.ValueKind != JsonValueKind.Array)
            return null;

        var items = new List<string>();
        foreach (var c in chars.EnumerateArray())
        {
            if (c.ValueKind == JsonValueKind.String)
            {
                var label = PortugueseCharacteristicName(c.GetString());
                if (label is not null)
                    items.Add(label);
            }
        }

        if (items.Count == 0)
            return null;

        return string.Join(", ", items);
    }

    /// <summary>
    /// Extract the postal code from a Century21 address string.
    /// Century21 addresses follow the pattern: "Street, NºX, XXXX-XXX Locality, Portugal"
    /// </summary>
    private static string? ExtractPostalCode(string? address)
    {
        if (string.IsNullOrEmpty(address))
            return null;

        // Match 4 digits, dash, 3 digits (XXXX-XXX)
        var match = System.Text.RegularExpressions.Regex.Match(address, @"\b(\d{4}-\d{3})\b");
        return match.Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Extract year_built from characteristics (not directly available as a field).
    /// Century21 doesn't expose year built in the list API.
    /// </summary>
    private static int? ExtractYearBuilt(JsonElement item)
    {
        // Not available in the list endpoint — characteristics don't include year built.
        return null;
    }

    /// <summary>
    /// Parse the entered_market date string.
    /// Format: "2026-06-03T00:00:00.000Z"
    /// </summary>
    private static DateTime ParseEnteredMarket(string? enteredMarket)
    {
        if (string.IsNullOrEmpty(enteredMarket))
            return DateTime.UtcNow;

        if (DateTime.TryParse(enteredMarket, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal, out var dt))
            return dt;

        return DateTime.UtcNow;
    }

    /// <summary>
    /// Extract image URLs from the images array.
    /// </summary>
    private static List<string> ExtractImages(JsonElement item)
    {
        if (!item.TryGetProperty("images", out var images) || images.ValueKind != JsonValueKind.Array)
            return [];

        var result = new List<string>(images.GetArrayLength());
        foreach (var img in images.EnumerateArray())
        {
            if (img.ValueKind == JsonValueKind.String)
            {
                var url = img.GetString();
                if (!string.IsNullOrEmpty(url))
                    result.Add(url);
            }
        }

        return result;
    }

    // ── Type mapping ─────────────────────────────────────────────

    private static PropertyType MapAssetType(string? assetType) => assetType switch
    {
        "house"       => PropertyType.House,
        "apartment"   => PropertyType.Apartment,
        "land"        => PropertyType.Land,
        "urban_land"  => PropertyType.Land,
        "store"       => PropertyType.Commercial,
        "warehouse"   => PropertyType.Commercial,
        "office"      => PropertyType.Commercial,
        _             => PropertyType.Other,
    };

    private static TransactionType MapAdType(string? adType) => adType switch
    {
        "sell" => TransactionType.Sale,
        "rent" => TransactionType.Rent,
        _      => TransactionType.Sale,
    };

    // ── Display helpers ──────────────────────────────────────────

    private static string PortuguesePropertyTypeName(string? assetType) => assetType switch
    {
        "house"       => "Moradia",
        "apartment"   => "Apartamento",
        "land"        => "Terreno",
        "urban_land"  => "Terreno Urbano",
        "store"       => "Loja",
        "warehouse"   => "Armazém",
        "office"      => "Escritório",
        _             => "Imóvel",
    };

    /// <summary>
    /// Map CamelCase characteristic keys to human-readable Portuguese labels.
    /// Prefixes like "has_", "with_" and suffixes are stripped; the core is
    /// translated when possible.
    /// </summary>
    private static string? PortugueseCharacteristicName(string? characteristic)
    {
        if (string.IsNullOrEmpty(characteristic))
            return null;

        return characteristic switch
        {
            "front_porch"          => "Alpendre",
            "storage"              => "Arrumos",
            "bathroom"             => "Casa de banho",
            "suite_bathroom"       => "Suite",
            "barbecue"             => "Churrasqueira",
            "bedrooms_hall"        => "Hall dos quartos",
            "kitchen"              => "Cozinha",
            "laundrette"           => "Lavandaria",
            "bedroom"              => "Quarto",
            "living_room"          => "Sala de estar",
            "dining_room"          => "Sala de jantar",
            "road_access"          => "Acesso à estrada",
            "good_light_exposure"  => "Boa exposição solar",
            "good_location"        => "Localização privilegiada",
            "quiet_place"          => "Zona sossegada",
            "central_location"     => "Localização central",
            "ground_floor"         => "Rés-do-chão",
            "high_ceilings"        => "Pés-direitos altos",
            "garage"               => "Garagem",
            "garden"               => "Jardim",
            "pool"                 => "Piscina",
            "fireplace"            => "Lareira",
            "air_conditioning"     => "Ar condicionado",
            "central_heating"      => "Aquecimento central",
            "double_glazing"       => "Vidros duplos",
            "balcony"              => "Varanda",
            "terrace"              => "Terraço",
            "elevator"             => "Elevador",
            "furnished"            => "Mobiliado",
            "equipped_kitchen"     => "Cozinha equipada",
            "rehabilitation"       => "Para reabilitação",
            "new"                  => "Novo",
            "under_construction"   => "Em construção",
            _                      => characteristic.Replace("_", " "),
        };
    }

    // ── JSON helpers ─────────────────────────────────────────────

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
}
