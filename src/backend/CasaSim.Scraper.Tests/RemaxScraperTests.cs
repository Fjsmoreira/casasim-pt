using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.Extensions.Logging.Abstractions;
using System.Net;

namespace CasaSim.Scraper.Tests;

public sealed class RemaxScraperTests
{
    private const string FixturesDir = "Fixtures";
    private const string ExpectedImageBase = "https://i.maxwork.pt/ds-l/";
    private const string SitemapIndexUrl = "https://remax.pt/sitemap.xml";
    private const string DetailSitemapUrl = "https://remax.pt/sitemap/listings_details_pt_1.xml";
    private const string PombalListingUrl = "https://www.remax.pt/pt/imoveis/venda-moradia-t2-pombal-abiul/122591135-5";
    private const string OtherCityListingUrl = "https://www.remax.pt/pt/imoveis/venda-moradia-t2-leiria-leiria/122591999-1";

    // ── Helpers ─────────────────────────────────────────────────

    private static RemaxListingParser CreateParser() => new();

    // ── ParseFromHtml ────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromHtml_ValidFixture_ReturnsProperty()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "remax-detail-122591135-5.html"));
        var parser = CreateParser();

        var prop = parser.ParseFromHtml(html);

        Assert.NotNull(prop);
        Assert.Equal("122591135-5", prop!.ExternalId);
        Assert.Equal("Remax", prop.SourceAgency);
        Assert.Equal(30000m, prop.Price);
        Assert.Equal(PropertyType.House, prop.Type);
        Assert.Equal(TransactionType.Sale, prop.Transaction);
    }

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromHtml_NoNextData_ReturnsNull()
    {
        var parser = CreateParser();
        var prop = parser.ParseFromHtml("<html><body>no next data here</body></html>");
        Assert.Null(prop);
    }

    // ── ParseFromJson: core fields ───────────────────────────────

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromJson_ReturnsPropertyWithCorrectFields()
    {
        var json = """
            {
                "listingTitle": "demo-listing-001",
                "businessType": "Venda",
                "listingType": "Apartamento",
                "listingPrice": 185000.0,
                "numberOfBedrooms": 3,
                "numberOfWC": 2,
                "totalArea": 120.0,
                "address": "Rua Principal",
                "doorNumber": "N\u00ba15",
                "regionName3": "Abiul",
                "regionName2": "Pombal",
                "regionName1": "Leiria",
                "zipCode": "3100-069",
                "latitude": 39.827,
                "longitude": -8.638,
                "descriptions": [
                    {
                        "languageCode": "PT",
                        "description": "Apartamento T3 em excelente estado"
                    }
                ]
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal("demo-listing-001", prop!.ExternalId);
        Assert.Equal("Remax", prop.SourceAgency);
        Assert.Equal(185000m, prop.Price);
        Assert.Equal(3, prop.Bedrooms);
        Assert.Equal(120.0, prop.AreaM2);
        Assert.Equal("Pombal", prop.City);
        Assert.Equal("Leiria", prop.District);
        Assert.Equal("Rua Principal, N\u00ba15", prop.Address);
        Assert.Equal("3100-069", prop.PostalCode);
        Assert.Equal(PropertyType.Apartment, prop.Type);
        Assert.Equal(TransactionType.Sale, prop.Transaction);
        Assert.Equal(2, prop.Bathrooms);
        Assert.NotNull(prop.Location);
        Assert.Equal(-8.638, prop.Location!.X, 6);
        Assert.Equal(39.827, prop.Location!.Y, 6);
        Assert.NotNull(prop.Description);
        Assert.Contains("Apartamento T3", prop.Description);
        Assert.DoesNotContain("<p>", prop.Description);
    }

    // ── ParseFromJson: listingPictures ───────────────────────────

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromJson_ListingPictures_ExtractsImageUrls()
    {
        var json = """
            {
                "listingTitle": "pic-test-001",
                "businessType": "Venda",
                "listingType": "Moradia",
                "listingPrice": 250000.0,
                "listingPictures": [
                    "listings/002/L_pic1.jpg",
                    "listings/002/L_pic2.jpg",
                    "listings/002/L_pic3.jpg"
                ],
                "listingPictureUrl": "listings/002/L_main.jpg"
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        // 4 images: 3 from listingPictures + 1 main (deduped if main is already in listingPictures)
        Assert.Equal(4, prop!.Images.Count);
        Assert.All(prop.Images, url => Assert.StartsWith(ExpectedImageBase, url));
        // Main picture should be first
        Assert.Contains("L_main", prop.Images[0]);
    }

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromJson_ListingPicturesAsObjects_ExtractsUrls()
    {
        var json = """
            {
                "listingTitle": "pic-obj-test",
                "businessType": "Venda",
                "listingType": "Moradia",
                "listingPrice": 300000.0,
                "listingPictures": [
                    { "url": "listings/003/L_pic1.jpg" },
                    { "url": "https://i.maxwork.pt/ds-l/listings/003/L_pic2.jpg" }
                ],
                "listingPictureUrl": "listings/003/L_main.jpg"
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal(3, prop!.Images.Count);
        Assert.All(prop.Images, url => Assert.StartsWith(ExpectedImageBase, url));
    }

    // ── ParseFromJson: missing optional fields ───────────────────

    [Fact]
    [Trait("Category", "Parser")]
    public void ParseFromJson_MissingOptionalFields_DoesNotThrow()
    {
        var json = """
            {
                "listingTitle": "minimal-001",
                "businessType": "Venda",
                "listingType": "Terreno",
                "listingPrice": 50000.0
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal("minimal-001", prop!.ExternalId);
        Assert.Equal(50000m, prop.Price);
        Assert.Equal(PropertyType.Land, prop.Type);
        // Optional fields should be defaults
        Assert.Null(prop.Bedrooms);
        Assert.Null(prop.Bathrooms);
        Assert.Null(prop.AreaM2);
        Assert.Null(prop.YearBuilt);
        Assert.Null(prop.EnergyClass);
        Assert.Null(prop.Location);
        Assert.Empty(prop.Images);
        Assert.Null(prop.Description);
        Assert.Null(prop.ListingUrl);
    }

    // ── ParseFromJson: businessType → TransactionType ────────────

    [Theory]
    [InlineData("Venda", TransactionType.Sale)]
    [InlineData("Arrendamento", TransactionType.Rent)]
    [InlineData("Rent", TransactionType.Rent)]
    [InlineData("Lease", TransactionType.Rent)]
    [InlineData("unknown_type", TransactionType.Sale)]
    [Trait("Category", "Parser")]
    public void ParseFromJson_BusinessType_MapsCorrectly(string businessType, TransactionType expected)
    {
        var json = $$"""
            {
                "listingTitle": "bt-test-001",
                "businessType": "{{businessType}}",
                "listingType": "Apartamento",
                "listingPrice": 100000.0
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal(expected, prop!.Transaction);
    }

    // ── ParseFromJson: listingType → PropertyType ────────────────

    [Theory]
    [InlineData("Moradia", PropertyType.House)]
    [InlineData("House", PropertyType.House)]
    [InlineData("Apartamento", PropertyType.Apartment)]
    [InlineData("Apartment", PropertyType.Apartment)]
    [InlineData("Terreno", PropertyType.Land)]
    [InlineData("Land", PropertyType.Land)]
    [InlineData("Villa", PropertyType.Villa)]
    [InlineData("Moradia Geminada", PropertyType.Townhouse)]
    [InlineData("Townhouse", PropertyType.Townhouse)]
    [InlineData("Comercial", PropertyType.Commercial)]
    [InlineData("Loja", PropertyType.Commercial)]
    [InlineData("Commerce", PropertyType.Commercial)]
    [InlineData("unknown_type", PropertyType.Other)]
    [Trait("Category", "Parser")]
    public void ParseFromJson_ListingType_MapsPropertyTypeCorrectly(string listingType, PropertyType expected)
    {
        var json = $$"""
            {
                "listingTitle": "pt-test-001",
                "businessType": "Venda",
                "listingType": "{{listingType}}",
                "listingPrice": 100000.0
            }
            """;

        var parser = CreateParser();
        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal(expected, prop!.Type);
    }

    // ── ScrapeAsync: sitemap discovery + detail fetch ───────────

    [Fact]
    [Trait("Category", "Scraper")]
    public async Task ScrapeAsync_SitemapDiscovery_FiltersDeduplicatesAndParsesPombalListings()
    {
        var detailHtml = File.ReadAllText(Path.Combine(FixturesDir, "remax-detail-122591135-5.html"));
        using var handler = new RoutingHandler(new Dictionary<string, HttpResponseMessage>
        {
            [SitemapIndexUrl] = TextResponse("""
                <?xml version="1.0" encoding="UTF-8"?>
                <sitemapindex>
                  <sitemap><loc>https://remax.pt/sitemap/listings_details_pt_1.xml</loc></sitemap>
                  <sitemap><loc>https://remax.pt/sitemap/pages_pt.xml</loc></sitemap>
                </sitemapindex>
                """),
            [DetailSitemapUrl] = TextResponse($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <urlset>
                  <url><loc>{PombalListingUrl}</loc></url>
                  <url><loc>{PombalListingUrl}</loc></url>
                  <url><loc>{OtherCityListingUrl}</loc></url>
                </urlset>
                """),
            [PombalListingUrl] = TextResponse(detailHtml),
        });

        using var http = new HttpClient(handler);
        var scraper = new RemaxScraper(http, CreateParser(), NullLogger<RemaxScraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var property = Assert.Single(properties);
        Assert.Equal("122591135-5", property.ExternalId);
        Assert.Equal(PombalListingUrl, property.ListingUrl);
        Assert.Equal(30000m, property.Price);
        Assert.Equal(PropertyType.House, property.Type);
        Assert.Equal(TransactionType.Sale, property.Transaction);
        Assert.Equal(1, handler.RequestCount(PombalListingUrl));
        Assert.Equal(0, handler.RequestCount(OtherCityListingUrl));
    }

    [Fact]
    [Trait("Category", "Scraper")]
    public async Task ScrapeAsync_DetailFailure_SkipsFailedListingAndKeepsProcessing()
    {
        var detailHtml = File.ReadAllText(Path.Combine(FixturesDir, "remax-detail-122591135-5.html"));
        const string failingListingUrl = "https://www.remax.pt/pt/imoveis/venda-apartamento-t2-pombal-pombal/124631157-21";
        using var handler = new RoutingHandler(new Dictionary<string, HttpResponseMessage>
        {
            [SitemapIndexUrl] = TextResponse("""
                <?xml version="1.0" encoding="UTF-8"?>
                <sitemapindex>
                  <sitemap><loc>https://remax.pt/sitemap/listings_details_pt_1.xml</loc></sitemap>
                </sitemapindex>
                """),
            [DetailSitemapUrl] = TextResponse($"""
                <?xml version="1.0" encoding="UTF-8"?>
                <urlset>
                  <url><loc>{failingListingUrl}</loc></url>
                  <url><loc>{PombalListingUrl}</loc></url>
                </urlset>
                """),
            [failingListingUrl] = new HttpResponseMessage(HttpStatusCode.InternalServerError),
            [PombalListingUrl] = TextResponse(detailHtml),
        });

        using var http = new HttpClient(handler);
        var scraper = new RemaxScraper(http, CreateParser(), NullLogger<RemaxScraper>.Instance);

        var properties = await scraper.ScrapeAsync(CancellationToken.None);

        var property = Assert.Single(properties);
        Assert.Equal("122591135-5", property.ExternalId);
        Assert.Equal(1, handler.RequestCount(failingListingUrl));
        Assert.Equal(1, handler.RequestCount(PombalListingUrl));
    }

    private static HttpResponseMessage TextResponse(string content) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(content),
    };

    private sealed class RoutingHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, HttpResponseMessage> _responses;
        private readonly Dictionary<string, int> _requestCounts = new(StringComparer.OrdinalIgnoreCase);

        public RoutingHandler(Dictionary<string, HttpResponseMessage> responses)
        {
            _responses = responses;
        }

        public int RequestCount(string url) =>
            _requestCounts.GetValueOrDefault(url);

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri?.ToString() ?? string.Empty;
            _requestCounts[url] = RequestCount(url) + 1;

            if (_responses.TryGetValue(url, out var response))
                return await CloneAsync(response, cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        private static async Task<HttpResponseMessage> CloneAsync(
            HttpResponseMessage response,
            CancellationToken cancellationToken)
        {
            var clone = new HttpResponseMessage(response.StatusCode);
            if (response.Content is not null)
                clone.Content = new StringContent(await response.Content.ReadAsStringAsync(cancellationToken));
            return clone;
        }
    }
}
