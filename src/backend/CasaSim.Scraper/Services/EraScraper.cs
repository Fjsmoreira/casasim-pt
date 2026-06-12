using System.Text.Json;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CasaSim.Scraper.Services;

/// <summary>
/// ERA Pombal scraper — discovers listings via the ERA Portugal REST API,
/// then fetches detail pages and parses them with <see cref="EraListingParser"/>.
///
/// ERA's REST API requires ASP.NET anti-forgery tokens (__RequestVerificationToken),
/// so each scrape cycle starts by acquiring a fresh token from the homepage before
/// calling the Property/Search API endpoint.
///
/// Search:  POST https://www.era.pt/API/ServicesModule/Property/Search
///             (requires __RequestVerificationToken cookie + header)
/// Detail:  GET  https://www.era.pt/imovel/{slugified-title}-{id}
/// </summary>
internal sealed class EraScraper : IPropertyScraper, IAgencyScraper
{
    private const string BaseUrl = "https://www.era.pt";
    private const string ApiBaseUrl = "https://www.era.pt/API/ServicesModule";
    private const string SearchEndpoint = "/Property/Search";
    private const string HomePagePath = "/";
    private const string AgencySlugValue = "era-pombal";
    private const string PombalSearchText = "Pombal";
    private const int RecordsPerPage = 50;

    // ── IPropertyScraper ────────────────────────────────────────

    public string AgencyName => "ERA";

    // ── IAgencyScraper ──────────────────────────────────────────

    string IAgencyScraper.AgencySlug => AgencySlugValue;

    ScraperSourceConfig IAgencyScraper.Config => new()
    {
        AgencyName = "ERA",
        AgencySlug = AgencySlugValue,
        BaseUrl = BaseUrl,
        ApiBaseUrl = ApiBaseUrl,
        SearchEndpoint = SearchEndpoint,
        DefaultSearchParams = new()
        {
            ["searchtext"] = PombalSearchText,
        },
    };

    // ── Dependencies ────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly EraListingParser _parser;
    private readonly ILogger<EraScraper> _logger;

    public EraScraper(
        HttpClient http,
        EraListingParser parser,
        ILogger<EraScraper> logger)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
    }

    // ── IPropertyScraper.ScrapeAsync ────────────────────────────

    /// <summary>
    /// Full scrape cycle: acquire anti-forgery token, search for Pombal
    /// listings via the API, then fetch and parse detail pages.
    /// </summary>
    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();

        try
        {
            // Step 1: Acquire anti-forgery token
            var token = await AcquireAntiForgeryTokenAsync(ct);
            if (string.IsNullOrEmpty(token))
            {
                _logger.LogError("Failed to acquire anti-forgery token for ERA API");
                return results;
            }

            // Step 2: Search for listings. The current ERA detail pages are
            // React-rendered enough that the legacy HTML parser no longer sees
            // the listing payload, but the search API already returns the fields
            // needed for CasaSim cards (title, price, areas, gallery, URL, etc.).
            results.AddRange(await SearchPropertyCardsAsync(token, ct));

            _logger.LogInformation("ERA search found {Count} listing(s)", results.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scrape ERA Pombal listings");
        }

        _logger.LogInformation("ERA total: {Count} properties", results.Count);
        return results;
    }

    // ── IAgencyScraper ──────────────────────────────────────────

    async Task<ScrapeResult> IAgencyScraper.ScrapeAllAsync(CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var errors = new List<ScrapeError>();
        var listings = new List<ParsedListing>();
        var totalFound = 0;

        try
        {
            var token = await AcquireAntiForgeryTokenAsync(ct);
            if (string.IsNullOrEmpty(token))
            {
                errors.Add(new ScrapeError
                {
                    AgencyName = AgencyName,
                    Message = "Failed to acquire anti-forgery token for ERA API",
                    ExceptionType = "TokenAcquisitionException",
                    Severity = ScrapeErrorSeverity.Error,
                });
                return new ScrapeResult
                {
                    AgencyName = AgencyName,
                    AgencySlug = AgencySlugValue,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow,
                    Listings = listings,
                    Errors = errors,
                    TotalFound = 0,
                };
            }

            var searchResults = await SearchPropertiesAsync(token, ct);
            totalFound = searchResults.Count;

            foreach (var (id, url) in searchResults)
            {
                try
                {
                    var html = await FetchDetailHtmlAsync(url, ct);
                    if (string.IsNullOrEmpty(html))
                        continue;

                    var parsed = _parser.ParseFromHtml(html, url);
                    if (parsed is not null)
                        listings.Add(parsed);
                }
                catch (Exception ex)
                {
                    errors.Add(new ScrapeError
                    {
                        AgencyName = AgencyName,
                        Message = $"Failed to fetch/parse ERA listing {id}: {ex.Message}",
                        ExceptionType = ex.GetType().Name,
                        StackTrace = ex.ToString(),
                        Severity = ScrapeErrorSeverity.Error,
                    });
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add(new ScrapeError
            {
                AgencyName = AgencyName,
                Message = $"ERA scrape cycle failed: {ex.Message}",
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.ToString(),
                Severity = ScrapeErrorSeverity.Error,
            });
        }

        return new ScrapeResult
        {
            AgencyName = AgencyName,
            AgencySlug = AgencySlugValue,
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Listings = listings,
            Errors = errors,
            TotalFound = totalFound,
        };
    }

    async Task<ScrapeResult> IAgencyScraper.ScrapeSearchAsync(string? searchParams, CancellationToken ct)
    {
        // ERA has a single API that returns search + enough data for listing cards.
        // For search-only mode, we return what we get from the API without fetching details.
        var startedAt = DateTime.UtcNow;
        var errors = new List<ScrapeError>();

        try
        {
            var token = await AcquireAntiForgeryTokenAsync(ct);
            if (string.IsNullOrEmpty(token))
            {
                errors.Add(new ScrapeError
                {
                    AgencyName = AgencyName,
                    Message = "Failed to acquire anti-forgery token for ERA API",
                    ExceptionType = "TokenAcquisitionException",
                    Severity = ScrapeErrorSeverity.Error,
                });
                return new ScrapeResult
                {
                    AgencyName = AgencyName,
                    AgencySlug = AgencySlugValue,
                    StartedAt = startedAt,
                    CompletedAt = DateTime.UtcNow,
                    Listings = [],
                    Errors = errors,
                    TotalFound = 0,
                };
            }

            var searchResults = await SearchPropertiesAsync(token, ct);
            // For search-only mode, build minimal ParsedListing entries from search data
            // (IDs and URLs only — details come from ScrapeDetailAsync or ScrapeAllAsync)
            var listings = searchResults.Select(sr => new ParsedListing
            {
                ExternalId = sr.Id,
                ListingUrl = sr.Url,
                Status = PropertyStatus.Active,
            }).ToList();

            return new ScrapeResult
            {
                AgencyName = AgencyName,
                AgencySlug = AgencySlugValue,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Listings = listings,
                Errors = errors,
                TotalFound = searchResults.Count,
            };
        }
        catch (Exception ex)
        {
            errors.Add(new ScrapeError
            {
                AgencyName = AgencyName,
                Message = $"ERA search failed: {ex.Message}",
                ExceptionType = ex.GetType().Name,
                StackTrace = ex.ToString(),
                Severity = ScrapeErrorSeverity.Error,
            });

            return new ScrapeResult
            {
                AgencyName = AgencyName,
                AgencySlug = AgencySlugValue,
                StartedAt = startedAt,
                CompletedAt = DateTime.UtcNow,
                Listings = [],
                Errors = errors,
                TotalFound = 0,
            };
        }
    }

    async Task<ParsedListing?> IAgencyScraper.ScrapeDetailAsync(string externalId, string? sourceUrl, CancellationToken ct)
    {
        // If sourceUrl is provided, fetch that page directly.
        var url = !string.IsNullOrEmpty(sourceUrl)
            ? sourceUrl
            : $"{BaseUrl}/imovel/p-{externalId}";

        try
        {
            var html = await FetchDetailHtmlAsync(url, ct);
            if (string.IsNullOrEmpty(html))
                return null;

            return _parser.ParseFromHtml(html, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch ERA detail for {Id} at {Url}", externalId, url);
            return null;
        }
    }

    // ── Internal: API token acquisition ─────────────────────────

    /// <summary>
    /// Fetch the ERA homepage and extract the __RequestVerificationToken
    /// from cookies and/or the page HTML.
    /// </summary>
    private async Task<string?> AcquireAntiForgeryTokenAsync(CancellationToken ct)
    {
        try
        {
            var response = await _http.GetAsync($"{BaseUrl}{HomePagePath}", ct);

            // Try cookie first
            if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
            {
                foreach (var cookie in cookies)
                {
                    var parts = cookie.Split(';');
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (trimmed.StartsWith("__RequestVerificationToken=", StringComparison.OrdinalIgnoreCase))
                        {
                            var token = trimmed["__RequestVerificationToken=".Length..];
                            if (!string.IsNullOrEmpty(token))
                            {
                                _logger.LogDebug("Acquired ERA anti-forgery token from cookie");
                                return token;
                            }
                        }
                    }
                }
            }

            // Fallback: try extracting from the response body.  ERA currently
            // renders the token as a hidden ASP.NET MVC input near the end of the
            // page:
            //   <input name="__RequestVerificationToken" ... value="..." />
            var body = await response.Content.ReadAsStringAsync(ct);
            var inputToken = ExtractHiddenRequestVerificationToken(body);
            if (!string.IsNullOrEmpty(inputToken))
            {
                _logger.LogDebug("Acquired ERA anti-forgery token from hidden input");
                return inputToken;
            }

            var tokenMarker = "var antiForgeryToken = '";
            var idx = body.IndexOf(tokenMarker, StringComparison.Ordinal);
            if (idx >= 0)
            {
                var start = idx + tokenMarker.Length;
                var end = body.IndexOf("'", start, StringComparison.Ordinal);
                if (end > start)
                {
                    var token = body[start..end];
                    _logger.LogDebug("Acquired ERA anti-forgery token from page body");
                    return token;
                }
            }

            _logger.LogWarning("Could not extract ERA anti-forgery token");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire ERA anti-forgery token");
            return null;
        }
    }

    // ── Internal: Search API ────────────────────────────────────

    /// <summary>
    /// Call the ERA Property/Search API and return discovered listing (id, url) pairs.
    /// Requires a valid __RequestVerificationToken.
    /// </summary>
    private async Task<List<(string Id, string Url)>> SearchPropertiesAsync(
        string token, CancellationToken ct)
    {
        var allResults = new List<(string Id, string Url)>();
        int page = 1;

        while (true)
        {
            var url = $"{ApiBaseUrl}{SearchEndpoint}";

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "searchtext": "{{PombalSearchText}}",
                      "page": {{page}},
                      "recordsPerPage": {{RecordsPerPage}},
                      "businessTypeIds": null,
                      "propertyTypeIds": null,
                      "propertySubTypeIds": null,
                      "category": null,
                      "agencyId": null,
                      "order": null,
                      "zoneIds": null,
                      "vantagensERA": null,
                      "projectIds": null,
                      "minPrice": null,
                      "maxPrice": null,
                      "minArea": null,
                      "maxArea": null
                    }
                    """,
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };

            request.Headers.Add("RequestVerificationToken", token);
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");

            _logger.LogDebug("ERA page {Page}: POST {Url} (token length: {Len})", page, url, token.Length);

            var response = await _http.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var pageResults = ParseSearchResponse(json);

            _logger.LogDebug("ERA page {Page}: {Count} items", page, pageResults.Count);

            allResults.AddRange(pageResults);

            if (pageResults.Count < RecordsPerPage)
                break;

            page++;

            if (page > 20)
                break;
        }

        _logger.LogInformation("ERA search: {Total} items across {Pages} page(s)", allResults.Count, page);
        return allResults;
    }

    private async Task<List<Property>> SearchPropertyCardsAsync(string token, CancellationToken ct)
    {
        var url = $"{ApiBaseUrl}{SearchEndpoint}";

        HttpRequestMessage BuildRequest(string requestToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(
                    $$"""
                    {
                      "searchtext": "{{PombalSearchText}}",
                      "page": 1,
                      "recordsPerPage": {{RecordsPerPage}},
                      "businessTypeIds": null,
                      "propertyTypeIds": null,
                      "propertySubTypeIds": null,
                      "category": null,
                      "agencyId": null,
                      "order": null,
                      "zoneIds": null,
                      "vantagensERA": null,
                      "projectIds": null,
                      "minPrice": null,
                      "maxPrice": null,
                      "minArea": null,
                      "maxArea": null
                    }
                    """,
                    System.Text.Encoding.UTF8,
                    "application/json"),
            };

            request.Headers.Add("RequestVerificationToken", requestToken);
            request.Headers.Add("X-Requested-With", "XMLHttpRequest");
            return request;
        }

        var response = await _http.SendAsync(BuildRequest(token), ct);
        if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            _logger.LogWarning("ERA search token was rejected; refetching token and retrying once");
            response.Dispose();
            var freshToken = await AcquireAntiForgeryTokenAsync(ct);
            response = await _http.SendAsync(BuildRequest(freshToken), ct);
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        return ParseSearchProperties(json);
    }

    /// <summary>
    /// Parse the ERA Property/Search API response to extract (id, detailUrl) pairs.
    /// </summary>
    internal static List<(string Id, string Url)> ParseSearchResponse(string json)
    {
        var results = new List<(string Id, string Url)>();

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        // The response may be an array directly, wrapped in a { data: [...] }
        // object, or (current ERA API) a { PropertyList: [...] } object.
        JsonElement.ArrayEnumerator items;

        if (root.ValueKind == JsonValueKind.Array)
        {
            items = root.EnumerateArray();
        }
        else if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            items = data.EnumerateArray();
        }
        else if (root.TryGetProperty("PropertyList", out var propertyList) && propertyList.ValueKind == JsonValueKind.Array)
        {
            items = propertyList.EnumerateArray();
        }
        else
        {
            return results;
        }

        foreach (var item in items)
        {
            var id = GetStringProp(item, "Id") ?? GetStringProp(item, "id") ?? GetStringProp(item, "PropertyId");
            if (string.IsNullOrEmpty(id))
                continue;

            // Try to build the detail URL from available fields.
            // ERA detail URLs follow: /imovel/{slugified-title}-{id}
            var detailUrl = GetStringProp(item, "DetailUrl") ?? GetStringProp(item, "Url");

            if (string.IsNullOrEmpty(detailUrl))
            {
                var titleSlug = GetStringProp(item, "TitleSlug") ?? GetStringProp(item, "titleSlug");
                if (!string.IsNullOrEmpty(titleSlug))
                {
                    detailUrl = $"https://www.era.pt/imovel/{titleSlug}-{id}";
                }
                else
                {
                    // Fallback: just use ID
                    detailUrl = $"https://www.era.pt/imovel/p-{id}";
                }
            }
            else if (!detailUrl.StartsWith("http"))
            {
                detailUrl = $"https://www.era.pt{detailUrl}";
            }

            results.Add((id, detailUrl));
        }

        return results;
    }

    internal static List<Property> ParseSearchProperties(string json)
    {
        var results = new List<Property>();
        using var doc = JsonDocument.Parse(json);
        if (!TryGetSearchItems(doc.RootElement, out var items))
            return results;

        foreach (var item in items)
        {
            var id = GetStringProp(item, "Id") ?? GetStringProp(item, "Reference");
            if (string.IsNullOrWhiteSpace(id))
                continue;

            var url = GetStringProp(item, "DetailUrl") ?? GetStringProp(item, "Url") ?? $"{BaseUrl}/imovel/p-{id}";
            if (!url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                url = $"{BaseUrl}{url}";

            var rentPrice = GetDecimalProp(item, "RentPrice");
            var salePrice = GetDecimalProp(item, "SellPrice");
            var subleasePrice = GetDecimalProp(item, "SubleasePrice");
            var lat = GetDoubleProp(item, "Lat");
            var lng = GetDoubleProp(item, "Lng");

            results.Add(new Property
            {
                ExternalId = id,
                SourceAgency = "ERA",
                Title = GetStringProp(item, "Title") ?? $"ERA listing {id}",
                Price = rentPrice ?? salePrice ?? subleasePrice ?? 0m,
                Currency = "EUR",
                Type = MapEraPropertyType(GetStringProp(item, "PropertyType"), GetStringProp(item, "PropertySubType")),
                Transaction = rentPrice is not null ? TransactionType.Rent : TransactionType.Sale,
                City = "Pombal",
                District = "Leiria",
                AreaM2 = GetDoubleProp(item, "ListingArea") ?? GetDoubleProp(item, "NetArea"),
                LandAreaM2 = GetDoubleProp(item, "LandArea"),
                Bedrooms = GetIntProp(item, "Rooms"),
                Bathrooms = GetIntProp(item, "Wcs"),
                ParkingSpots = GetIntProp(item, "Parking"),
                EnergyClass = GetStringProp(item, "Ce"),
                Images = GetGalleryUrls(item),
                ListingUrl = url,
                Status = GetBoolProp(item, "IsSold") == true ? PropertyStatus.Sold : PropertyStatus.Active,
                Location = lat.HasValue && lng.HasValue ? new Point(lng.Value, lat.Value) { SRID = 4326 } : null,
            });
        }

        return results;
    }

    private static bool TryGetSearchItems(JsonElement root, out JsonElement.ArrayEnumerator items)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            items = root.EnumerateArray();
            return true;
        }

        if (root.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
        {
            items = data.EnumerateArray();
            return true;
        }

        if (root.TryGetProperty("PropertyList", out var propertyList) && propertyList.ValueKind == JsonValueKind.Array)
        {
            items = propertyList.EnumerateArray();
            return true;
        }

        items = default;
        return false;
    }

    // ── Internal: Detail page fetching ──────────────────────────

    /// <summary>
    /// Fetch and parse a single ERA detail page.
    /// Returns the mapped Property or null on failure.
    /// </summary>
    private async Task<Property?> FetchAndParseDetailAsync(string detailUrl, string id, CancellationToken ct)
    {
        var html = await FetchDetailHtmlAsync(detailUrl, ct);
        if (string.IsNullOrEmpty(html))
            return null;

        var parsed = _parser.ParseFromHtml(html, detailUrl);
        if (parsed is null)
        {
            _logger.LogWarning("Failed to parse ERA listing {Id} from {Url}", id, detailUrl);
            return null;
        }

        return MapToProperty(parsed);
    }

    /// <summary>
    /// Fetch the HTML of a ERA detail page.
    /// </summary>
    private async Task<string?> FetchDetailHtmlAsync(string detailUrl, CancellationToken ct)
    {
        _logger.LogDebug("GET {Url}", detailUrl);

        var response = await _http.GetAsync(detailUrl, ct);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(ct);
    }

    // ── Mapping ─────────────────────────────────────────────────

    /// <summary>
    /// Convert a <see cref="ParsedListing"/> (output of <see cref="EraListingParser"/>)
    /// to a <see cref="Property"/> (input of <see cref="ListingUpsertService"/>).
    /// </summary>
    private static Property MapToProperty(ParsedListing listing)
    {
        var property = new Property
        {
            ExternalId = listing.ExternalId,
            SourceAgency = "ERA",
            Title = listing.Title,
            Description = listing.Description,
            Price = listing.Price,
            Currency = listing.Currency,
            Type = listing.Type,
            Transaction = listing.Transaction,
            Address = listing.Address,
            City = listing.City ?? "Pombal",
            District = listing.District ?? "Leiria",
            PostalCode = listing.PostalCode,
            AreaM2 = listing.AreaM2,
            LandAreaM2 = listing.LandAreaM2,
            Bedrooms = listing.Bedrooms,
            Bathrooms = listing.Bathrooms,
            ParkingSpots = listing.ParkingSpots,
            YearBuilt = listing.YearBuilt,
            EnergyClass = listing.EnergyClass,
            Images = listing.Images,
            ListingUrl = listing.ListingUrl,
            DiscoveredAt = listing.DiscoveredAt,
            Status = listing.Status,
        };

        if (listing.Latitude.HasValue && listing.Longitude.HasValue)
        {
            property.Location = new Point(listing.Longitude.Value, listing.Latitude.Value) { SRID = 4326 };
        }

        return property;
    }

    // ── JSON helper ─────────────────────────────────────────────

    private static string? GetStringProp(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var val))
            return null;

        return GetJsonElementAsString(val);
    }

    private static string? GetJsonElementAsString(JsonElement val)
    {
        return val.ValueKind switch
        {
            JsonValueKind.String => val.GetString(),
            JsonValueKind.Number => val.TryGetInt64(out var n) ? n.ToString(System.Globalization.CultureInfo.InvariantCulture) : val.GetRawText(),
            _ => null,
        };
    }

    private static decimal? GetDecimalProp(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var val))
            return null;

        var text = val.ValueKind == JsonValueKind.Object && val.TryGetProperty("Value", out var objectValue)
            ? GetJsonElementAsString(objectValue)
            : GetJsonElementAsString(val);

        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace(" ", string.Empty).Replace("€", string.Empty);
        var ptText = text;
        if (ptText.Contains('.') && !ptText.Contains(','))
            ptText = ptText.Replace(".", string.Empty);
        if (decimal.TryParse(ptText, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-PT"), out var pt))
            return pt;
        if (decimal.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        return null;
    }

    private static double? GetDoubleProp(JsonElement el, string property)
    {
        var text = GetStringProp(el, property);
        if (string.IsNullOrWhiteSpace(text))
            return null;

        text = text.Replace(" ", string.Empty);
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var invariant))
            return invariant;
        if (double.TryParse(text, System.Globalization.NumberStyles.Any, new System.Globalization.CultureInfo("pt-PT"), out var pt))
            return pt;
        return null;
    }

    private static int? GetIntProp(JsonElement el, string property)
    {
        var value = GetDoubleProp(el, property);
        return value.HasValue ? (int)Math.Round(value.Value) : null;
    }

    private static bool? GetBoolProp(JsonElement el, string property)
    {
        if (!el.TryGetProperty(property, out var val))
            return null;

        return val.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(val.GetString(), out var b) => b,
            _ => null,
        };
    }

    private static List<string> GetGalleryUrls(JsonElement item)
    {
        if (!item.TryGetProperty("Gallery", out var gallery) || gallery.ValueKind != JsonValueKind.Array)
            return [];

        var urls = new List<string>();
        foreach (var image in gallery.EnumerateArray())
        {
            var url = GetStringProp(image, "Url");
            if (!string.IsNullOrWhiteSpace(url))
                urls.Add(url);
        }

        return urls;
    }

    private static PropertyType MapEraPropertyType(string? propertyType, string? propertySubType)
    {
        var text = $"{propertyType} {propertySubType}".ToLowerInvariant();
        if (text.Contains("apartamento")) return PropertyType.Apartment;
        if (text.Contains("moradia")) return PropertyType.House;
        if (text.Contains("terreno") || text.Contains("lote")) return PropertyType.Land;
        if (text.Contains("loja") || text.Contains("armaz") || text.Contains("escrit")) return PropertyType.Commercial;
        return PropertyType.Other;
    }

    private static string? ExtractHiddenRequestVerificationToken(string html)
    {
        const string nameMarker = "name=\"__RequestVerificationToken\"";
        var nameIdx = html.IndexOf(nameMarker, StringComparison.OrdinalIgnoreCase);
        if (nameIdx < 0)
            return null;

        var tagEnd = html.IndexOf('>', nameIdx);
        if (tagEnd <= nameIdx)
            return null;

        var tag = html[nameIdx..tagEnd];
        const string valueMarker = "value=\"";
        var valueIdx = tag.IndexOf(valueMarker, StringComparison.OrdinalIgnoreCase);
        if (valueIdx < 0)
            return null;

        var start = valueIdx + valueMarker.Length;
        var end = tag.IndexOf('"', start);
        return end > start ? tag[start..end] : null;
    }
}
