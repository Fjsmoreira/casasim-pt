using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Century21 Pombal scraper — fetches listings from the Century21 Portugal
/// public REST API and returns them as <see cref="Property"/> instances
/// ready for DB upsert.
///
/// The Century21 list endpoint returns full listing data in a single call,
/// so no separate detail-page fetch is required.  We make two calls per
/// cycle (sell + rent) to capture both transaction types.
///
/// API base: https://www.century21.pt/api
/// List:     GET /api/properties?addresses=1015&amp;address_names=Pombal&amp;ad_type=sell
/// </summary>
internal sealed class Century21Scraper : IPropertyScraper, IAgencyScraper
{
    private const string BaseUrl = "https://www.century21.pt";
    private const string ApiPath = "/api/properties";
    private const string PombalAddressId = "1015";
    private const string PombalAddressName = "Pombal";

    // ── IPropertyScraper ────────────────────────────────────────

    public string AgencyName => "Century21";

    // ── IAgencyScraper ──────────────────────────────────────────

    string IAgencyScraper.AgencySlug => "century21-pombal";

    ScraperSourceConfig IAgencyScraper.Config => new()
    {
        AgencyName = "Century21",
        AgencySlug = "century21-pombal",
        BaseUrl = BaseUrl,
        ApiBaseUrl = BaseUrl,
        SearchEndpoint = ApiPath,
        DefaultSearchParams = new()
        {
            ["addresses"] = PombalAddressId,
            ["address_names"] = PombalAddressName,
        },
    };

    // ── Dependencies ────────────────────────────────────────────

    private readonly HttpClient _http;
    private readonly Century21ListingParser _parser;
    private readonly ILogger<Century21Scraper> _logger;

    public Century21Scraper(
        HttpClient http,
        Century21ListingParser parser,
        ILogger<Century21Scraper> logger)
    {
        _http = http;
        _parser = parser;
        _logger = logger;
    }

    // ── IPropertyScraper.ScrapeAsync ────────────────────────────

    /// <summary>
    /// Fetch ALL Century21 Pombal listings (sell + rent) in a single call.
    /// Returns normalized <see cref="Property"/> instances ready for upsert.
    /// Errors from one transaction type don't block the other.
    /// </summary>
    public async Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default)
    {
        var results = new List<Property>();

        // Sell
        try
        {
            var sell = await FetchAdTypeAsync("sell", ct);
            results.AddRange(sell);
            _logger.LogInformation("Century21 sell: {Count} properties", sell.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Century21 sell listings");
        }

        // Rent
        try
        {
            var rent = await FetchAdTypeAsync("rent", ct);
            results.AddRange(rent);
            _logger.LogInformation("Century21 rent: {Count} properties", rent.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch Century21 rent listings");
        }

        _logger.LogInformation("Century21 total: {Count} properties", results.Count);
        return results;
    }

    // ── IAgencyScraper ──────────────────────────────────────────

    async Task<ScrapeResult> IAgencyScraper.ScrapeAllAsync(CancellationToken ct)
    {
        var startedAt = DateTime.UtcNow;
        var errors = new List<ScrapeError>();
        var listings = new List<ParsedListing>();
        var totalFound = 0;

        foreach (var adType in new[] { "sell", "rent" })
        {
            try
            {
                var (parsed, total) = await FetchRawAsync(adType, ct);
                listings.AddRange(parsed);
                totalFound += total;
            }
            catch (Exception ex)
            {
                errors.Add(new ScrapeError
                {
                    AgencyName = AgencyName,
                    Message = $"Failed to fetch {adType} listings: {ex.Message}",
                    ExceptionType = ex.GetType().Name,
                    StackTrace = ex.ToString(),
                    Severity = ScrapeErrorSeverity.Error,
                });
            }
        }

        return new ScrapeResult
        {
            AgencyName = AgencyName,
            AgencySlug = "century21-pombal",
            StartedAt = startedAt,
            CompletedAt = DateTime.UtcNow,
            Listings = listings,
            Errors = errors,
            TotalFound = totalFound,
        };
    }

    async Task<ScrapeResult> IAgencyScraper.ScrapeSearchAsync(string? searchParams, CancellationToken ct)
    {
        // Century21 list endpoint returns full data — no separate search phase.
        // Delegate to ScrapeAllAsync and ignore the searchParams hint.
        return await ((IAgencyScraper)this).ScrapeAllAsync(ct);
    }

    async Task<ParsedListing?> IAgencyScraper.ScrapeDetailAsync(string externalId, string? sourceUrl, CancellationToken ct)
    {
        // The list endpoint already returns full data — we don't have a
        // separate detail API.  Return null to signal "not available in
        // detail-only mode"; the full cycle covers everything.
        _logger.LogDebug("ScrapeDetailAsync called for {Id} — Century21 has no detail endpoint, returning null", externalId);
        return await Task.FromResult<ParsedListing?>(null);
    }

    // ── Internal ─────────────────────────────────────────────────

    /// <summary>
    /// Fetch one ad_type (sell/rent), parse, map to Property.
    /// </summary>
    private async Task<IReadOnlyList<Property>> FetchAdTypeAsync(string adType, CancellationToken ct)
    {
        var (parsed, _) = await FetchRawAsync(adType, ct);
        return parsed.Select(MapToProperty).ToList();
    }

    /// <summary>
    /// Fetch one ad_type (sell/rent) and return raw parsed listings + total count.
    /// Century21 paginates at 20 items/page.  This method loops through all
    /// pages to collect every listing in the concelho.
    /// </summary>
    private async Task<(IReadOnlyList<ParsedListing> Listings, int Total)> FetchRawAsync(
        string adType, CancellationToken ct)
    {
        const int pageSize = 20;
        var allListings = new List<ParsedListing>();
        int totalCount = 0;
        int page = 1;

        do
        {
            var url = $"{BaseUrl}{ApiPath}?addresses={PombalAddressId}&address_names={PombalAddressName}&ad_type={adType}&page={page}";

            _logger.LogDebug("C21 {AdType} page {Page}: GET {Url}", adType, page, url);

            var response = await _http.GetAsync(url, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);

            // Parse response to extract total + page listings
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("total", out var totalEl) && totalEl.ValueKind == System.Text.Json.JsonValueKind.Number)
                totalCount = totalEl.GetInt32();

            var parsed = _parser.ParseFromApiResponse(json);
            // Deduplicate by ExternalId — Century21 can return the same
            // listing on multiple pages (pagination overlap).
            foreach (var listing in parsed)
            {
                if (!allListings.Any(l => l.ExternalId == listing.ExternalId))
                    allListings.Add(listing);
            }

            if (totalCount > 0 && allListings.Count >= totalCount)
                break;

            page++;
        }
        while (allListings.Count > 0 && page <= 20); // safety limit

        _logger.LogInformation("Century21 {AdType}: {Fetched}/{Total} properties across {Pages} page(s)",
            adType, allListings.Count, totalCount, page);

        return (allListings, totalCount > 0 ? totalCount : allListings.Count);
    }

    // ── Mapping ──────────────────────────────────────────────────

    /// <summary>
    /// Convert a <see cref="ParsedListing"/> (output of the parser) to a
    /// <see cref="Property"/> (input of <see cref="ListingUpsertService"/>).
    /// </summary>
    private static Property MapToProperty(ParsedListing listing)
    {
        var property = new Property
        {
            ExternalId = listing.ExternalId,
            SourceAgency = "Century21",
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
            PublishedAt = listing.PublishedAt,
            DiscoveredAt = listing.DiscoveredAt,
            Status = listing.Status,
        };

        if (listing.Latitude.HasValue && listing.Longitude.HasValue)
        {
            property.Location = new Point(listing.Longitude.Value, listing.Latitude.Value) { SRID = 4326 };
        }

        return property;
    }
}
