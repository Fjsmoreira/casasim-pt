using System.Net;
using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace CasaSim.Scraper.Tests;

public sealed class Century21ScraperTests
{
    private const string FixturesDir = "Fixtures";

    // ── Mapping: single listing fixtures → Property ─────────────

    [Fact]
    public async Task ScrapeAsync_SellListFixture_MapsAllProperties()
    {
        using var handler = CreateSingleResponseHandler("century21-sell-list.json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.NotEmpty(properties);
        Assert.All(properties, p => Assert.False(string.IsNullOrEmpty(p.ExternalId)));
        Assert.All(properties, p => Assert.Equal("EUR", p.Currency));
        Assert.All(properties, p => Assert.Equal("Century21", p.SourceAgency));
        Assert.All(properties, p => Assert.Equal(TransactionType.Sale, p.Transaction));
        Assert.Contains(properties, p => p.Type == PropertyType.House);
        Assert.Contains(properties, p => p.Type == PropertyType.Apartment);
        Assert.Contains(properties, p => p.Type == PropertyType.Land);
        Assert.Contains(properties, p => p.Type == PropertyType.Commercial);
        Assert.All(properties, p => Assert.False(string.IsNullOrEmpty(p.Title)));
        Assert.All(properties, p => Assert.StartsWith("https://www.century21.pt/", p.ListingUrl));
        Assert.All(properties, p => Assert.True(p.Price >= 0));
    }

    [Fact]
    public async Task ScrapeAsync_RentListFixture_MapsToRentTransaction()
    {
        using var handler = CreateSingleResponseHandler("century21-rent-list.json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.NotEmpty(properties);
        Assert.All(properties, p => Assert.Equal(TransactionType.Rent, p.Transaction));
    }

    [Fact]
    public async Task ScrapeAsync_HouseFixture_MapsAllFields()
    {
        var parser = new Century21ListingParser();
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-house-0563-01902.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("0563-01902", prop.ExternalId);
        Assert.Equal("Moradia T3 Térrea com garagem e Jardim", prop.Title);
        Assert.Equal(395000m, prop.Price);
        Assert.Equal("EUR", prop.Currency);
        Assert.Equal("Century21", prop.SourceAgency);
        Assert.Equal(PropertyType.House, prop.Type);
        Assert.Equal(TransactionType.Sale, prop.Transaction);

        Assert.NotNull(prop.Location);
        Assert.Equal(-8.76565217971802, prop.Location!.X, 10);
        Assert.Equal(39.9463482516022, prop.Location!.Y, 10);
        Assert.Equal(4326, prop.Location.SRID);

        Assert.Equal("R. da Charneca 5, 3105-187 Guia, Portugal", prop.Address);
        Assert.Equal("Pombal", prop.City);
        Assert.Equal("Leiria", prop.District);
        Assert.Equal("3105-187", prop.PostalCode);
        Assert.Equal(190.99, prop.AreaM2);
        Assert.Null(prop.LandAreaM2);
        Assert.Equal(3, prop.Bedrooms);
        Assert.Equal(3, prop.Bathrooms);
        Assert.Equal(2, prop.ParkingSpots);
        Assert.Null(prop.YearBuilt);
        Assert.Null(prop.EnergyClass);
        Assert.True(prop.Images.Count >= 30);
        Assert.All(prop.Images, url => Assert.StartsWith("https://images.century21.pt/", url));
        Assert.Equal("https://www.century21.pt/ref/0563-01902", prop.ListingUrl);
        Assert.Equal(PropertyStatus.Active, prop.Status);
        Assert.NotNull(prop.Description);
        Assert.Contains("Alpendre", prop.Description);
        Assert.Contains("Churrasqueira", prop.Description);
    }

    [Fact]
    public async Task ScrapeAsync_ApartmentFixture_MapsCorrectly()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-apartment-C0381-00891.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("C0381-00891", prop.ExternalId);
        Assert.Equal(PropertyType.Apartment, prop.Type);
        Assert.Equal(289000m, prop.Price);
        Assert.Equal(3, prop.Bedrooms);
        Assert.Equal(2, prop.Bathrooms);
        Assert.Equal(132.0, prop.AreaM2);
    }

    [Fact]
    public async Task ScrapeAsync_LandFixture_MapsCorrectly()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-land-0739-04558.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("0739-04558", prop.ExternalId);
        Assert.Equal(PropertyType.Land, prop.Type);
        Assert.Equal(TransactionType.Sale, prop.Transaction);
        Assert.Equal(57500m, prop.Price);
        Assert.Null(prop.Bedrooms);
        Assert.Null(prop.Bathrooms);
    }

    [Fact]
    public async Task ScrapeAsync_StoreFixture_MapsToCommercial()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-store-C0381-00854.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("C0381-00854", prop.ExternalId);
        Assert.Equal(PropertyType.Commercial, prop.Type);
        Assert.Equal(92500m, prop.Price);
        Assert.Equal(117.0, prop.AreaM2);
        Assert.Equal(2, prop.Bathrooms);
    }

    [Fact]
    public async Task ScrapeAsync_WarehouseFixture_MapsToCommercial()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-warehouse-C0381-00858.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("C0381-00858", prop.ExternalId);
        Assert.Equal(PropertyType.Commercial, prop.Type);
        Assert.Equal(270000m, prop.Price);
        Assert.Equal(210.0, prop.AreaM2);
        Assert.Equal(1, prop.Bathrooms);
    }

    [Fact]
    public async Task ScrapeAsync_OfficeRentFixture_MapsToCommercialRent()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-office-C0381-00900.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("C0381-00900", prop.ExternalId);
        Assert.Equal(PropertyType.Commercial, prop.Type);
        Assert.Equal(TransactionType.Rent, prop.Transaction);
        Assert.Equal(325m, prop.Price);
        Assert.Equal(60.0, prop.AreaM2);
    }

    [Fact]
    public async Task ScrapeAsync_RentHouseNoPrice_DefaultsToZero()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-rent-house-C0381-00908.json"));
        using var handler = CreateSingleRawHandler(WrapInListResponse(json));
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var prop = Assert.Single(properties);
        Assert.Equal("C0381-00908", prop.ExternalId);
        Assert.Equal(PropertyType.House, prop.Type);
        Assert.Equal(TransactionType.Rent, prop.Transaction);
        Assert.Equal(0m, prop.Price);
        Assert.Equal(2, prop.Bedrooms);
        Assert.Equal(2, prop.Bathrooms);
    }

    // ── Edge cases ──────────────────────────────────────────────

    [Fact]
    public async Task ScrapeAsync_EmptyResponse_ReturnsEmpty()
    {
        using var handler = CreateSingleRawHandler("{}");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(properties);
    }

    [Fact]
    public async Task ScrapeAsync_HttpError_ReturnsEmpty()
    {
        using var handler = new FailingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://www.century21.pt") };
        var parser = new Century21ListingParser();
        var scraper = new Century21Scraper(http, parser, NullLogger<Century21Scraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        Assert.Empty(properties);
    }

    // ── Helpers ─────────────────────────────────────────────────

    /// <summary>
    /// Returns the fixture for the first request, empty for the second.
    /// The scraper makes 2 requests (sell + rent); without this, both
    /// get the same fixture and produce duplicate listings.
    /// </summary>
    private static HttpMessageHandler CreateSingleResponseHandler(string fixtureFile)
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, fixtureFile));
        return new FirstThenEmptyHandler(json, "{}");
    }

    private static HttpMessageHandler CreateSingleRawHandler(string content)
    {
        return new FirstThenEmptyHandler(content, "{}");
    }

    private static string WrapInListResponse(string singleListingJson)
    {
        return $"{{\"data\":[{singleListingJson}],\"total\":1}}";
    }

    /// <summary>
    /// Returns <c>firstResponse</c> for the first request and
    /// <c>secondResponse</c> for all subsequent requests.
    /// </summary>
    private sealed class FirstThenEmptyHandler : HttpMessageHandler
    {
        private readonly string _firstResponse;
        private readonly string _secondResponse;
        private bool _isFirst = true;

        public FirstThenEmptyHandler(string firstResponse, string secondResponse)
        {
            _firstResponse = firstResponse;
            _secondResponse = secondResponse;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
        {
            if (_isFirst)
            {
                _isFirst = false;
                return Task.FromResult(OkResponse(_firstResponse));
            }
            return Task.FromResult(OkResponse(_secondResponse));
        }

        private static HttpResponseMessage OkResponse(string content)
            => new(HttpStatusCode.OK) { Content = new StringContent(content) };
    }

    /// <summary>
    /// Always returns InternalServerError.
    /// </summary>
    private sealed class FailingHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken ct)
            => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
    }
}
