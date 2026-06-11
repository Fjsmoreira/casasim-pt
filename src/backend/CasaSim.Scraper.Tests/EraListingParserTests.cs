using CasaSim.Core.Models;
using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class EraListingParserTests
{
    private const string FixturesDir = "Fixtures";

    // ── Apartment (sale) ─────────────────────────────────────────

    [Fact]
    public void ParseFromHtml_ApartmentSell_MapsAllFields()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "era-apartment-404260053.html"));
        const string sourceUrl = "https://www.era.pt/imovel/apartamento-t3-pombal-pombal-404260053";
        var parser = new EraListingParser();

        var listing = parser.ParseFromHtml(html, sourceUrl);

        Assert.NotNull(listing);
        Assert.Equal("404260053", listing!.ExternalId);
        Assert.Equal(258000m, listing.Price);
        Assert.Equal("EUR", listing.Currency);
        Assert.Equal(PropertyType.Apartment, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);

        // Location
        Assert.Equal("Pombal", listing.City);
        Assert.Equal("Leiria", listing.District);
        // "POMBAL, Pombal" — same place repeated, so parish is empty
        Assert.Equal(string.Empty, listing.Parish);

        // Title
        Assert.Contains("Apartamento", listing.Title);
        Assert.Contains("T3", listing.Title);
        Assert.Contains("Pombal", listing.Title);

        // Area — uses usable (101) over gross (149)
        Assert.Equal(101.0, listing.AreaM2);
        Assert.Null(listing.LandAreaM2);

        // Rooms
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal(1, listing.ParkingSpots);

        // Energy class
        Assert.Equal("C", listing.EnergyClass);

        // No year built from ERA detail pages
        Assert.Null(listing.YearBuilt);

        // Listing URL
        Assert.Equal(sourceUrl, listing.ListingUrl);

        // Status
        Assert.Equal(PropertyStatus.Active, listing.Status);

        // Images
        Assert.True(listing.Images.Count >= 3, $"Expected at least 3 images, got {listing.Images.Count}");
        Assert.All(listing.Images, url => Assert.StartsWith("https://media", url));
    }

    // ── House (sale, Sob Consulta) ───────────────────────────────

    [Fact]
    public void ParseFromHtml_HouseSell_SobConsulta_ParsesCorrectly()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "era-house-404260052.html"));
        const string sourceUrl = "https://www.era.pt/imovel/moradia-t3-pombal-abiul-404260052";
        var parser = new EraListingParser();

        var listing = parser.ParseFromHtml(html, sourceUrl);

        Assert.NotNull(listing);
        Assert.Equal("404260052", listing!.ExternalId);
        Assert.Equal(PropertyType.House, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);

        // "Sob Consulta" means price is 0
        Assert.Equal(0m, listing.Price);

        // Location
        Assert.Equal("Pombal", listing.City);
        Assert.Equal("Leiria", listing.District);
        Assert.Equal("Abiul", listing.Parish);

        // Title
        Assert.Contains("Moradia", listing.Title);
        Assert.Contains("T3", listing.Title);
        Assert.Contains("Abiul", listing.Title);

        // Area
        Assert.Equal(100.0, listing.AreaM2);

        // Land area
        Assert.Equal(3131.0, listing.LandAreaM2);

        // Rooms
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal(2, listing.ParkingSpots);

        // No floor shown for houses — no energy class in this fixture
        Assert.Null(listing.EnergyClass);

        // Listing URL
        Assert.Equal(sourceUrl, listing.ListingUrl);
    }

    // ── Edge cases ───────────────────────────────────────────────

    [Fact]
    public void ParseFromHtml_WithoutSourceUrl_Works()
    {
        var html = File.ReadAllText(Path.Combine(FixturesDir, "era-apartment-404260053.html"));
        var parser = new EraListingParser();

        var listing = parser.ParseFromHtml(html);

        Assert.NotNull(listing);
        Assert.Equal("404260053", listing!.ExternalId);
        Assert.Equal(258000m, listing.Price);
        Assert.Null(listing.ListingUrl);
    }

    [Fact]
    public void ParseFromHtml_InvalidHtml_ReturnsNull()
    {
        var parser = new EraListingParser();
        var listing = parser.ParseFromHtml("<html><body>no property data here</body></html>");
        Assert.Null(listing);
    }

    [Fact]
    public void ParseFromHtml_EmptyString_ReturnsNull()
    {
        var parser = new EraListingParser();
        var listing = parser.ParseFromHtml("");
        Assert.Null(listing);
    }

    [Fact]
    public void ParseFromHtml_MissingReference_ReturnsNull()
    {
        var html = """
            <html><body>
            <div class="property-place"><span>Apartamento, POMBAL, Pombal</span></div>
            </body></html>
            """;
        var parser = new EraListingParser();
        var listing = parser.ParseFromHtml(html);
        Assert.Null(listing);
    }
}
