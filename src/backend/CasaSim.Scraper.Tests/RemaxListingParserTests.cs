using CasaSim.Core.Models;
using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class RemaxListingParserTests
{
    private const string FixturesDir = "Fixtures";

    [Fact]
    public void ParseFromJson_RealListing_MapsAllFields()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "remax-listing-122591135-5.json"));
        var parser = new RemaxListingParser();

        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal("122591135-5", prop!.ExternalId);
        Assert.Equal("Remax", prop.SourceAgency);
        Assert.Equal(30000m, prop.Price);
        Assert.Equal("EUR", prop.Currency);
        Assert.Equal(PropertyType.House, prop.Type);
        Assert.Equal(TransactionType.Sale, prop.Transaction);

        // Title: Moradia T2 à venda em Abiul, Pombal
        Assert.Contains("Moradia", prop.Title);
        Assert.Contains("T2", prop.Title);
        Assert.Contains("Abiul", prop.Title);
        Assert.Contains("Pombal", prop.Title);

        // Description should be HTML-stripped
        Assert.NotNull(prop.Description);
        Assert.Contains("pedra", prop.Description);
        Assert.DoesNotContain("<p>", prop.Description);

        // Area
        Assert.Equal(78.0, prop.AreaM2);
        Assert.Equal(1290.0, prop.LandAreaM2);

        // Bedrooms / bathrooms
        Assert.Equal(2, prop.Bedrooms);
        Assert.Equal(0, prop.Bathrooms);

        // Year built
        Assert.Equal(1957, prop.YearBuilt);
        Assert.Equal(new DateTime(2017, 7, 21, 0, 0, 0, DateTimeKind.Utc), prop.PublishedAt);

        // Energy class: energyEfficiencyLevelID = 11 => "NC"
        Assert.Equal("NC", prop.EnergyClass);

        // Location
        Assert.NotNull(prop.Location);
        Assert.Equal(-8.63771343231201, prop.Location!.X, 10);
        Assert.Equal(39.8277626037598, prop.Location.Y, 10);

        // Address
        Assert.Equal("Rua da Escola, Nº27", prop.Address);
        Assert.Equal("Pombal", prop.City);
        Assert.Equal("Leiria", prop.District);
        Assert.Equal("3100-069", prop.PostalCode);

        // Images — should have the 5 listing pictures + 1 main picture (deduped)
        Assert.True(prop.Images.Count >= 5, $"Expected at least 5 images, got {prop.Images.Count}");
        Assert.All(prop.Images, url => Assert.StartsWith("https://i.maxwork.pt/ds-l/", url));
    }

    [Fact]
    public void ParseFromHtml_WithSourceUrl_ExtractsExternalIdFromSlug()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "remax-detail-122591135-5.html"));
        var parser = new RemaxListingParser();
        const string sourceUrl = "https://www.remax.pt/comprar/moradia/pombal/122591135-5";

        var prop = parser.ParseFromHtml(html, sourceUrl);

        Assert.NotNull(prop);
        Assert.Equal("122591135-5", prop!.ExternalId);
        Assert.Equal(30000m, prop.Price);
        Assert.Equal(PropertyType.House, prop.Type);
        Assert.Equal("Rua da Escola, Nº27", prop.Address);
        Assert.Equal("Pombal", prop.City);
        Assert.Equal(78.0, prop.AreaM2);
        Assert.Equal(2, prop.Bedrooms);
        Assert.Equal("NC", prop.EnergyClass);
        Assert.Equal(sourceUrl, prop.ListingUrl);
    }

    [Fact]
    public void ParseFromHtml_SyntheticFixture_WorksWithoutSourceUrl()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "remax-detail-122591135-5.html"));
        var parser = new RemaxListingParser();

        var prop = parser.ParseFromHtml(html);

        Assert.NotNull(prop);
        // Without sourceUrl, ExternalId falls back to listingTitle
        Assert.Equal("122591135-5", prop!.ExternalId);
        Assert.Equal(30000m, prop.Price);
    }

    [Fact]
    public void ParseFromJson_NullSourceUrl_FallsBackToListingTitle()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "remax-listing-122591135-5.json"));
        var parser = new RemaxListingParser();

        var prop = parser.ParseFromJson(json);

        Assert.NotNull(prop);
        Assert.Equal("122591135-5", prop!.ExternalId);
    }

    [Fact]
    public void ParseFromJson_WithSourceUrl_SetsListingUrl()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "remax-listing-122591135-5.json"));
        var parser = new RemaxListingParser();
        const string sourceUrl = "https://www.remax.pt/comprar/moradia/pombal/122591135-5";

        var prop = parser.ParseFromJson(json, sourceUrl);

        Assert.NotNull(prop);
        Assert.Equal(sourceUrl, prop!.ListingUrl);
    }

    [Fact]
    public void ParseFromHtml_InvalidHtml_ReturnsNull()
    {
        var parser = new RemaxListingParser();
        var prop = parser.ParseFromHtml("<html><body>no next data</body></html>");
        Assert.Null(prop);
    }

    [Fact]
    public void ParseFromJson_InvalidJson_Throws()
    {
        var parser = new RemaxListingParser();
        var ex = Record.Exception(() => parser.ParseFromJson("{invalid}"));
        Assert.NotNull(ex);
        Assert.IsAssignableFrom<System.Text.Json.JsonException>(ex);
    }
}
