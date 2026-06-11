using CasaSim.Core.Models;
using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class RemaxScraperTests
{
    private const string FixturesDir = "Fixtures";
    private const string ExpectedImageBase = "https://i.maxwork.pt/ds-l/";

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
}
