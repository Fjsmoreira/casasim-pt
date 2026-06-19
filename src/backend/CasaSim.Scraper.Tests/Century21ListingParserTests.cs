using CasaSim.Core.Models;
using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class Century21ListingParserTests
{
    private const string FixturesDir = "Fixtures";

    // ── House (sell) ────────────────────────────────────────────────

    [Fact]
    public void ParseSingle_HouseSell_MapsAllFields()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-house-0563-01902.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("0563-01902", listing!.ExternalId);
        Assert.Equal("Moradia T3 Térrea com garagem e Jardim", listing.Title);
        Assert.Equal(395000m, listing.Price);
        Assert.Equal("EUR", listing.Currency);
        Assert.Equal(PropertyType.House, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);

        // Location
        Assert.Equal("R. da Charneca 5, 3105-187 Guia, Portugal", listing.Address);
        Assert.Equal("Pombal", listing.City);
        Assert.Equal("Leiria", listing.District);
        Assert.Equal("3105-187", listing.PostalCode);
        Assert.Equal(39.9463482516022, listing!.Latitude!.Value, 10);
        Assert.Equal(-8.76565217971802, listing!.Longitude!.Value, 10);

        // Area — uses useful_area (190.99) over gross_area (272.5)
        Assert.Equal(190.99, listing.AreaM2);

        // Rooms
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(3, listing.Bathrooms);
        Assert.Equal(2, listing.ParkingSpots);

        // No year built available from list API
        Assert.Null(listing.YearBuilt);

        // No energy class available from list API
        Assert.Null(listing.EnergyClass);

        // Images
        Assert.True(listing.Images.Count >= 30, $"Expected at least 30 images, got {listing.Images.Count}");
        Assert.All(listing.Images, url => Assert.StartsWith("https://images.century21.pt/", url));

        // Listing URL
        Assert.Equal("https://www.century21.pt/ref/0563-01902", listing.ListingUrl);

        // Status
        Assert.Equal(PropertyStatus.Active, listing.Status);

        // DiscoveredAt from entered_market
        Assert.Equal(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc), listing.DiscoveredAt);
        Assert.Equal(new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc), listing.PublishedAt);

        // Description built from characteristics
        Assert.NotNull(listing.Description);
        Assert.Contains("Alpendre", listing.Description);
        Assert.Contains("Churrasqueira", listing.Description);
        Assert.Contains("Suite", listing.Description);
        Assert.Contains("Boa exposição solar", listing.Description);
    }

    // ── Apartment (sell) ────────────────────────────────────────────

    [Fact]
    public void ParseSingle_ApartmentSell_MapsCorrectly()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-apartment-C0381-00891.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00891", listing!.ExternalId);
        Assert.Equal(PropertyType.Apartment, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);
        Assert.Equal(289000m, listing.Price);
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
        Assert.Equal(132.0, listing.AreaM2);
    }

    // ── Land (sell) ─────────────────────────────────────────────────

    [Fact]
    public void ParseSingle_LandSell_MapsCorrectly()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-land-0739-04558.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("0739-04558", listing!.ExternalId);
        Assert.Equal(PropertyType.Land, listing.Type);
        Assert.Equal(TransactionType.Sale, listing.Transaction);
        Assert.Equal(57500m, listing.Price);
        // Land with zero area
        Assert.Equal(0.0, listing.AreaM2);
        // Land has no bedrooms/bathrooms
        Assert.Null(listing.Bedrooms);
        Assert.Null(listing.Bathrooms);
    }

    // ── Store (Commercial, sell) ────────────────────────────────────

    [Fact]
    public void ParseSingle_StoreSell_MapsToCommercial()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-store-C0381-00854.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00854", listing!.ExternalId);
        Assert.Equal(PropertyType.Commercial, listing.Type);
        Assert.Equal(92500m, listing.Price);
        Assert.Equal(117.0, listing.AreaM2);
        Assert.Equal(2, listing.Bathrooms);
    }

    // ── Warehouse (Commercial, sell) ────────────────────────────────

    [Fact]
    public void ParseSingle_WarehouseSell_MapsToCommercial()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-warehouse-C0381-00858.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00858", listing!.ExternalId);
        Assert.Equal(PropertyType.Commercial, listing.Type);
        Assert.Equal(270000m, listing.Price);
        Assert.Equal(210.0, listing.AreaM2);
        Assert.Equal(1, listing.Bathrooms);
    }

    // ── Urban Land (Land, sell) ─────────────────────────────────────

    [Fact]
    public void ParseSingle_UrbanLandSell_MapsToLand()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-urban-land-C0381-00872.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00872", listing!.ExternalId);
        Assert.Equal(PropertyType.Land, listing.Type);
        Assert.Equal(80000m, listing.Price);
        Assert.Equal(160.0, listing.AreaM2);
    }

    // ── Office (Commercial, rent) ──────────────────────────────────

    [Fact]
    public void ParseSingle_OfficeRent_MapsToCommercialRent()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-office-C0381-00900.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00900", listing!.ExternalId);
        Assert.Equal(PropertyType.Commercial, listing.Type);
        Assert.Equal(TransactionType.Rent, listing.Transaction);
        Assert.Equal(325m, listing.Price);
        Assert.Equal(60.0, listing.AreaM2);
    }

    // ── Rent House (no price) ──────────────────────────────────────

    [Fact]
    public void ParseSingle_RentHouseNoPrice_DefaultsToZero()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-rent-house-C0381-00908.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("C0381-00908", listing!.ExternalId);
        Assert.Equal(PropertyType.House, listing.Type);
        Assert.Equal(TransactionType.Rent, listing.Transaction);
        // Price is null in source data — default to 0
        Assert.Equal(0m, listing.Price);
        Assert.Equal(2, listing.Bedrooms);
        Assert.Equal(2, listing.Bathrooms);
    }

    // ── Full API response ─────────────────────────────────────────

    [Fact]
    public void ParseFromApiResponse_SellList_ParsesAllListings()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-sell-list.json"));
        var parser = new Century21ListingParser();

        var listings = parser.ParseFromApiResponse(json);

        Assert.NotEmpty(listings);
        Assert.All(listings, l => Assert.False(string.IsNullOrEmpty(l.ExternalId)));
        Assert.All(listings, l => Assert.Equal("EUR", l.Currency));
        Assert.All(listings, l => Assert.Equal(TransactionType.Sale, l.Transaction));
        Assert.All(listings, l => Assert.Equal("Pombal", l.City));
        Assert.All(listings, l => Assert.Equal("Leiria", l.District));
        Assert.All(listings, l => Assert.Equal(PropertyStatus.Active, l.Status));

        // At least one of each type
        Assert.Contains(listings, l => l.Type == PropertyType.House);
        Assert.Contains(listings, l => l.Type == PropertyType.Apartment);
        Assert.Contains(listings, l => l.Type == PropertyType.Land);
        Assert.Contains(listings, l => l.Type == PropertyType.Commercial);

        // Every listing should have a title
        Assert.All(listings, l => Assert.False(string.IsNullOrEmpty(l.Title)));

        // Every listing should have a listing URL
        Assert.All(listings, l => Assert.StartsWith("https://www.century21.pt/", l.ListingUrl));
    }

    [Fact]
    public void ParseFromApiResponse_RentList_ParsesAllListings()
    {
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-rent-list.json"));
        var parser = new Century21ListingParser();

        var listings = parser.ParseFromApiResponse(json);

        Assert.NotEmpty(listings);
        Assert.All(listings, l => Assert.Equal(TransactionType.Rent, l.Transaction));
    }

    // ── Edge cases ─────────────────────────────────────────────────

    [Fact]
    public void ParseSingle_InvalidJson_Throws()
    {
        var parser = new Century21ListingParser();
        var ex = Record.Exception(() => parser.ParseSingle("{invalid}"));
        Assert.NotNull(ex);
    }

    [Fact]
    public void ParseFromApiResponse_EmptyResponse_ReturnsEmpty()
    {
        var parser = new Century21ListingParser();
        var listings = parser.ParseFromApiResponse("{}");
        Assert.Empty(listings);
    }

    [Fact]
    public void ParseFromApiResponse_NoDataArray_ReturnsEmpty()
    {
        var parser = new Century21ListingParser();
        var listings = parser.ParseFromApiResponse("{\"total\": 0}");
        Assert.Empty(listings);
    }

    [Fact]
    public void ParseSingle_PostalCode_ExtractedFromAddress()
    {
        // The house fixture has "R. da Charneca 5, 3105-187 Guia, Portugal"
        var json = File.ReadAllText(Path.Combine(FixturesDir, "century21-house-0563-01902.json"));
        var parser = new Century21ListingParser();

        var listing = parser.ParseSingle(json);

        Assert.NotNull(listing);
        Assert.Equal("3105-187", listing!.PostalCode);
    }
}
