using System.Net;
using CasaSim.Core.Interfaces;
using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaSim.Scraper.Tests;

public sealed class EraScraperTests
{
    private const string FixturesDir = "Fixtures";
    private static readonly Uri BaseUri = new("https://www.era.pt");
    private const string FakeToken = "test-anti-forgery-token-value";

    // ── ParseSearchResponse ──────────────────────────────────────

    [Fact]
    public void ParseSearchResponse_JsonArray_ReturnsListings()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "era-search-response.json"));
        var results = EraScraper.ParseSearchResponse(json);

        Assert.Equal(2, results.Count);

        Assert.Equal("404260053", results[0].Id);
        Assert.Contains("404260053", results[0].Url);

        Assert.Equal("404260052", results[1].Id);
        Assert.Contains("404260052", results[1].Url);
    }

    [Fact]
    public void ParseSearchResponse_EmptyArray_ReturnsEmpty()
    {
        var results = EraScraper.ParseSearchResponse("[]");
        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_EmptyObject_ReturnsEmpty()
    {
        var results = EraScraper.ParseSearchResponse("{}");
        Assert.Empty(results);
    }

    [Fact]
    public void ParseSearchResponse_InvalidJson_Throws()
    {
        Assert.ThrowsAny<System.Text.Json.JsonException>(() =>
            EraScraper.ParseSearchResponse("not json"));
    }

    [Fact]
    public void ParseSearchResponse_ObjectWithDataArray_ReturnsListings()
    {
        var json = """
            {
              "data": [
                {"Id": "404260053", "Url": "/imovel/apartamento-t3-pombal-pombal-404260053"},
                {"Id": "404260052", "Url": "/imovel/moradia-t3-pombal-abiul-404260052"}
              ],
              "total": 2
            }
            """;
        var results = EraScraper.ParseSearchResponse(json);

        Assert.Equal(2, results.Count);
        Assert.Equal("404260053", results[0].Id);
        Assert.Equal("404260052", results[1].Id);
    }

    [Fact]
    public void ParseSearchResponse_ListingWithOnlyId_BuildsUrl()
    {
        var json = """
            [
              {"Id": "404260099"}
            ]
            """;
        var results = EraScraper.ParseSearchResponse(json);

        var (id, url) = Assert.Single(results);
        Assert.Equal("404260099", id);
        Assert.Contains("404260099", url);
    }

    // ── ScrapeDetailAsync (IAgencyScraper) ───────────────────────┘

    [Fact]
    public async Task ScrapeDetailAsync_FetchesAndParsesApartment()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "era-apartment-404260053.html"));
        using var handler = new SingleResponseHandler(html);
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var sourceUrl = "https://www.era.pt/imovel/apartamento-t3-pombal-pombal-404260053";
        var listing = await ((IAgencyScraper)scraper).ScrapeDetailAsync("404260053", sourceUrl, CancellationToken.None);

        Assert.NotNull(listing);
        Assert.Equal("404260053", listing!.ExternalId);
        Assert.Equal(258000m, listing.Price);
        Assert.Equal(PropertyType.Apartment, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);
        Assert.Equal("Pombal", listing.City);
        Assert.Equal("Leiria", listing.District);
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal(101.0, listing.AreaM2);
        Assert.Equal("C", listing.EnergyClass);
        Assert.Equal(sourceUrl, listing.ListingUrl);
    }

    [Fact]
    public async Task ScrapeDetailAsync_FetchesAndParsesHouse()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "era-house-404260052.html"));
        using var handler = new SingleResponseHandler(html);
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var sourceUrl = "https://www.era.pt/imovel/moradia-t3-pombal-abiul-404260052";
        var listing = await ((IAgencyScraper)scraper).ScrapeDetailAsync("404260052", sourceUrl, CancellationToken.None);

        Assert.NotNull(listing);
        Assert.Equal("404260052", listing!.ExternalId);
        Assert.Equal(0m, listing.Price); // Sob Consulta
        Assert.Equal(PropertyType.House, listing.Type);
        Assert.Equal("Pombal", listing.City);
        Assert.Equal("Leiria", listing.District);
        Assert.Equal("Abiul", listing.Parish);
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal(100.0, listing.AreaM2);
        Assert.Equal(3131.0, listing.LandAreaM2);
        Assert.Equal(sourceUrl, listing.ListingUrl);
    }

    [Fact]
    public async Task ScrapeDetailAsync_HttpError_ReturnsNull()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var listing = await ((IAgencyScraper)scraper).ScrapeDetailAsync("404260053", "https://www.era.pt/imovel/test", CancellationToken.None);

        Assert.Null(listing);
    }

    [Fact]
    public async Task ScrapeDetailAsync_InvalidHtml_ReturnsNull()
    {
        using var handler = new SingleResponseHandler("<html><body>no data</body></html>");
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var listing = await ((IAgencyScraper)scraper).ScrapeDetailAsync("999999", "https://www.era.pt/imovel/test", CancellationToken.None);

        Assert.Null(listing);
    }

    // ── ScrapeAsync (IPropertyScraper — full pipeline) ──────────

    [Fact]
    public async Task ScrapeAsync_FullPipeline_MapsAllProperties()
    {
        var searchJson = File.ReadAllText(Path.Combine(FixturesDir, "era-search-response.json"));
        var apartmentHtml = File.ReadAllText(Path.Combine(FixturesDir, "era-apartment-404260053.html"));
        var houseHtml = File.ReadAllText(Path.Combine(FixturesDir, "era-house-404260052.html"));

        using var handler = new EraPipelineHandler(
            searchResponse: searchJson,
            detailResponses: new Dictionary<string, string>
            {
                ["404260053"] = apartmentHtml,
                ["404260052"] = houseHtml,
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Equal(2, properties.Count);

        // Apartment
        var apt = properties.First(p => p.ExternalId == "404260053");
        Assert.Equal("ERA", apt.SourceAgency);
        Assert.Equal(258000m, apt.Price);
        Assert.Equal("EUR", apt.Currency);
        Assert.Equal(PropertyType.Apartment, apt.Type);
        Assert.Equal(TransactionType.Sale, apt.Transaction);
        Assert.Equal("Pombal", apt.City);
        Assert.Equal("Leiria", apt.District);
        Assert.Equal(3, apt.Bedrooms);
        Assert.Equal(101.0, apt.AreaM2);

        // House
        var house = properties.First(p => p.ExternalId == "404260052");
        Assert.Equal("ERA", house.SourceAgency);
        Assert.Equal(0m, house.Price); // Sob Consulta
        Assert.Equal(PropertyType.House, house.Type);
        Assert.Equal("Pombal", house.City);
        Assert.Equal(3, house.Bedrooms);
        Assert.Equal(100.0, house.AreaM2);
        Assert.Equal(3131.0, house.LandAreaM2);
    }

    [Fact]
    public async Task ScrapeAsync_EmptySearch_ReturnsEmpty()
    {
        using var handler = new EraPipelineHandler(
            searchResponse: "[]",
            detailResponses: new Dictionary<string, string>());
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(properties);
    }

    [Fact]
    public async Task ScrapeAsync_HttpErrorOnSearch_ReturnsEmpty()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(properties);
    }

    // ── IAgencyScraper.ScrapeAllAsync ──────────────────────────

    [Fact]
    public async Task ScrapeAllAsync_FullPipeline_ReturnsScrapeResult()
    {
        var searchJson = File.ReadAllText(Path.Combine(FixturesDir, "era-search-response.json"));
        var apartmentHtml = File.ReadAllText(Path.Combine(FixturesDir, "era-apartment-404260053.html"));
        var houseHtml = File.ReadAllText(Path.Combine(FixturesDir, "era-house-404260052.html"));

        using var handler = new EraPipelineHandler(
            searchResponse: searchJson,
            detailResponses: new Dictionary<string, string>
            {
                ["404260053"] = apartmentHtml,
                ["404260052"] = houseHtml,
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var result = await ((IAgencyScraper)scraper).ScrapeAllAsync(CancellationToken.None);

        Assert.Equal("ERA", result.AgencyName);
        Assert.Equal("era-pombal", result.AgencySlug);
        Assert.Equal(2, result.Listings.Count);
        Assert.Equal(2, result.TotalFound);
        Assert.Empty(result.Errors);
        Assert.True(result.StartedAt != default);
        Assert.True(result.CompletedAt != default);
        Assert.True(result.CompletedAt >= result.StartedAt);

        // Verify first listing
        var apt = result.Listings.First(l => l.ExternalId == "404260053");
        Assert.Equal(PropertyType.Apartment, apt.Type);
        Assert.Equal(258000m, apt.Price);
    }

    [Fact]
    public async Task ScrapeAllAsync_EmptySearch_ReturnsEmptyResult()
    {
        using var handler = new EraPipelineHandler(
            searchResponse: "[]",
            detailResponses: new Dictionary<string, string>());
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var result = await ((IAgencyScraper)scraper).ScrapeAllAsync(CancellationToken.None);

        Assert.Empty(result.Listings);
        Assert.Equal(0, result.TotalFound);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public async Task ScrapeAllAsync_HttpError_ReturnsResultWithError()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var result = await ((IAgencyScraper)scraper).ScrapeAllAsync(CancellationToken.None);

        Assert.Empty(result.Listings);
        Assert.Equal(0, result.TotalFound);
        Assert.NotEmpty(result.Errors);
    }

    // ── IAgencyScraper.ScrapeSearchAsync ────────────────────────

    [Fact]
    public async Task ScrapeSearchAsync_ReturnsListingsFromSearch()
    {
        var searchJson = File.ReadAllText(Path.Combine(FixturesDir, "era-search-response.json"));

        using var handler = new EraSearchOnlyHandler(searchResponse: searchJson);
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var parser = new EraListingParser();
        var scraper = new EraScraper(http, parser, NullLogger<EraScraper>.Instance);

        var result = await ((IAgencyScraper)scraper).ScrapeSearchAsync(null, CancellationToken.None);

        Assert.Equal(2, result.Listings.Count);
        Assert.Equal(2, result.TotalFound);
        Assert.Empty(result.Errors);
        // Search-only listings should have at least ExternalId
        Assert.All(result.Listings, l => Assert.False(string.IsNullOrEmpty(l.ExternalId)));
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>
    /// Returns a single canned response for every request.
    /// </summary>
    private sealed class SingleResponseHandler : HttpMessageHandler
    {
        private readonly string _response;
        public SingleResponseHandler(string response) => _response = response;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(OkResponse(_response));
    }

    /// <summary>
    /// Always returns 500.
    /// </summary>
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }

    /// <summary>
    /// Handles the full ERA pipeline:
    /// 1st request (homepage) → return Set-Cookie with anti-forgery token
    /// 2nd request (search API POST) → return search JSON
    /// Subsequent requests (detail page GETs) → return fixture HTML based on URL
    /// </summary>
    private sealed class EraPipelineHandler : HttpMessageHandler
    {
        private readonly string _searchResponse;
        private readonly Dictionary<string, string> _detailResponses;
        private int _callCount;

        public EraPipelineHandler(string searchResponse, Dictionary<string, string> detailResponses)
        {
            _searchResponse = searchResponse;
            _detailResponses = detailResponses;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            _callCount++;

            // 1st call: homepage → anti-forgery token
            if (_callCount == 1)
            {
                var response = OkResponse("<html><body>ERA homepage</body></html>");
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    $"__RequestVerificationToken={FakeToken}; path=/; HttpOnly");
                return Task.FromResult(response);
            }

            // 2nd call: search API → search JSON
            if (_callCount == 2)
            {
                return Task.FromResult(OkResponse(_searchResponse));
            }

            // 3rd+ calls: detail page fetches
            // Match by property ID in the URL
            foreach (var (id, html) in _detailResponses)
            {
                if (request.RequestUri?.AbsoluteUri.Contains(id) == true)
                {
                    return Task.FromResult(OkResponse(html));
                }
            }

            // Unknown URL
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
        }
    }

    /// <summary>
    /// Handles search-only flow (homepage → search API, no detail fetches).
    /// </summary>
    private sealed class EraSearchOnlyHandler : HttpMessageHandler
    {
        private readonly string _searchResponse;
        private bool _homepageDone;

        public EraSearchOnlyHandler(string searchResponse) => _searchResponse = searchResponse;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            if (!_homepageDone)
            {
                _homepageDone = true;
                var response = OkResponse("<html><body>ERA homepage</body></html>");
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    $"__RequestVerificationToken={FakeToken}; path=/; HttpOnly");
                return Task.FromResult(response);
            }

            return Task.FromResult(OkResponse(_searchResponse));
        }
    }

    private static HttpResponseMessage OkResponse(string content)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, System.Text.Encoding.UTF8),
            // Set a reasonable Content-Type for ERA pages
        };
}
