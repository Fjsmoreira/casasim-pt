using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Api.Services;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;

namespace CasaSim.Api.Tests;

public sealed class ListingsControllerTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateDb(string? dbName = null)
    {
        dbName ??= "ListingsTests_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);
        SeedData(db);
        return db;
    }

    private static void SeedData(AppDbContext db)
    {
        if (db.Agencies.Any()) return;

        db.Agencies.Add(new Agency
        {
            Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });

        db.Listings.AddRange(
            new Listing
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000001"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                ExternalId = "prop-001",
                Title = "T1 Apartment Pombal Centre",
                Price = 85000m,
                Currency = "EUR",
                PropertyType = ListingPropertyType.Apartment,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Active,
                City = "Pombal",
                Bedrooms = 1,
                AreaM2 = 55m,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new Listing
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000002"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                ExternalId = "prop-002",
                Title = "T3 House Abiul",
                Price = 185000m,
                Currency = "EUR",
                PropertyType = ListingPropertyType.House,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Active,
                City = "Abiul",
                Bedrooms = 3,
                AreaM2 = 150m,
                LandAreaM2 = 500m,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new Listing
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000003"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                ExternalId = "prop-003",
                Title = "T2 Apartment Rent Pombal",
                Price = 650m,
                Currency = "EUR",
                PropertyType = ListingPropertyType.Apartment,
                PriceType = ListingPriceType.Rent,
                Status = ListingStatus.Active,
                City = "Pombal",
                Bedrooms = 2,
                AreaM2 = 80m,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new Listing
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000004"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                ExternalId = "prop-004",
                Title = "T4 Villa Land Pombal",
                Price = 350000m,
                Currency = "EUR",
                PropertyType = ListingPropertyType.Villa,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Active,
                City = "Pombal",
                Bedrooms = 4,
                AreaM2 = 250m,
                LandAreaM2 = 1200m,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new Listing
            {
                Id = Guid.Parse("b1000000-0000-0000-0000-000000000005"),
                AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                ExternalId = "prop-005",
                Title = "Sold House Marinha das Ondas",
                Price = 125000m,
                Currency = "EUR",
                PropertyType = ListingPropertyType.House,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Sold,
                City = "Marinha das Ondas",
                Bedrooms = 2,
                AreaM2 = 100m,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            }
        );

        db.SaveChanges();
    }

    private static (ListingsController, AppDbContext) CreateController(string? dbName = null)
    {
        var db = CreateDb(dbName);
        var svc = new Mock<IListingQueryService>();
        return (new ListingsController(db, svc.Object), db);
    }

    // ──────────────────────────────────────────────────────────
    //  GET /api/listings/{id}
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetById_Existing_ReturnsOk()
    {
        var (ctrl, _) = CreateController();
        var id = Guid.Parse("b1000000-0000-0000-0000-000000000001");

        var result = await ctrl.GetById(id);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var listing = Assert.IsType<Listing>(ok.Value);
        Assert.Equal("T1 Apartment Pombal Centre", listing.Title);
        Assert.Equal(85000m, listing.Price);
        Assert.Equal(ListingPropertyType.Apartment, listing.PropertyType);
    }

    [Fact]
    public async Task GetById_NotExisting_ReturnsNotFound()
    {
        var (ctrl, _) = CreateController();
        var id = Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff");

        var result = await ctrl.GetById(id);

        Assert.IsType<NotFoundResult>(result.Result);
    }

    // ──────────────────────────────────────────────────────────
    //  GET /api/listings/geojson — data roundtrip
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGeoJson_WithData_ReturnsFeatureCollection()
    {
        // This test exercises the controller with actual data via the DB.
        // We need to seed listings with Location/Coordinate to appear in results.
        var dbName = "GeoJsonData_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);
        SeedData(db);

        // Add listings with spatial locations
        var agencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        db.Listings.Add(new Listing
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            ExternalId = "geo-001",
            Title = "Geo Apartment Pombal",
            Price = 100000m,
            PropertyType = ListingPropertyType.Apartment,
            PriceType = ListingPriceType.Sale,
            Status = ListingStatus.Active,
            City = "Pombal",
            Bedrooms = 2,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
            FirstSeenAt = SeedTime,
            LastSeenAt = SeedTime,
            Location = new CasaSim.Core.Data.Entities.Location
            {
                Id = Guid.NewGuid(),
                Municipality = "Pombal",
                District = "Leiria",
                CountryCode = "PT",
                Coordinate = new Point(-8.6283, 39.9167) { SRID = 4326 },
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            Images = new List<ListingImage>
            {
                new()
                {
                    Id = Guid.NewGuid(),
                    Url = "https://example.com/img.jpg",
                    IsPrimary = true,
                    SortOrder = 0,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    FirstSeenAt = SeedTime,
                    LastSeenAt = SeedTime,
                }
            }
        });

        // Add a non-active listing that should be excluded
        db.Listings.Add(new Listing
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            ExternalId = "geo-002",
            Title = "Sold House",
            Price = 200000m,
            PropertyType = ListingPropertyType.House,
            PriceType = ListingPriceType.Sale,
            Status = ListingStatus.Sold,
            City = "Pombal",
            Bedrooms = 3,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
            FirstSeenAt = SeedTime,
            LastSeenAt = SeedTime,
            Location = new CasaSim.Core.Data.Entities.Location
            {
                Id = Guid.NewGuid(),
                Municipality = "Pombal",
                District = "Leiria",
                CountryCode = "PT",
                Coordinate = new Point(-8.6200, 39.9100) { SRID = 4326 },
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
        });

        await db.SaveChangesAsync();

        var svc = new Mock<IListingQueryService>();
        var ctrl = new ListingsController(db, svc.Object);

        // Bounding box around Pombal
        var result = await ctrl.GetGeoJson(
            swLat: 39.5, swLng: -9.0, neLat: 40.5, neLng: -8.0,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);

        // Dynamically read the anonymous type
        var response = ok.Value;
        var typeProp = response.GetType().GetProperty("type")!.GetValue(response);
        var features = response.GetType().GetProperty("features")!.GetValue(response) as System.Collections.IEnumerable;

        Assert.Equal("FeatureCollection", typeProp);
        Assert.NotNull(features);

        // Should have 1 active listing (the sold one is excluded)
        var featureList = features.Cast<object>().ToList();
        Assert.Single(featureList);

        var feature = featureList[0];
        var featureType = feature.GetType().GetProperty("type")!.GetValue(feature);
        var geometry = feature.GetType().GetProperty("geometry")!.GetValue(feature);
        var properties = feature.GetType().GetProperty("properties")!.GetValue(feature);

        Assert.Equal("Feature", featureType);
        Assert.NotNull(geometry);
        Assert.NotNull(properties);

        // Geometry checks
        var geomType = geometry.GetType().GetProperty("type")!.GetValue(geometry);
        var coords = geometry.GetType().GetProperty("coordinates")!.GetValue(geometry) as double[];
        Assert.Equal("Point", geomType);
        Assert.NotNull(coords);
        Assert.Equal(2, coords.Length);
        Assert.Equal(-8.6283, coords[0], 4); // lng
        Assert.Equal(39.9167, coords[1], 4); // lat

        // Properties checks
        var propPrice = properties.GetType().GetProperty("price")!.GetValue(properties);
        var propCity = properties.GetType().GetProperty("city")!.GetValue(properties);
        Assert.Equal(100000m, propPrice);
        Assert.Equal("Pombal", propCity);
    }

    // ──────────────────────────────────────────────────────────
    //  GET /api/listings/geojson — empty result
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetGeoJson_NoMatchingListings_ReturnsEmptyFeatureCollection()
    {
        var dbName = "GeoJsonEmpty_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);
        SeedData(db);

        var svc = new Mock<IListingQueryService>();
        var ctrl = new ListingsController(db, svc.Object);

        // Bounding box far away from any data
        var result = await ctrl.GetGeoJson(
            swLat: 30.0, swLng: -20.0, neLat: 31.0, neLng: -19.0,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = ok.Value;
        var typeProp = response.GetType().GetProperty("type")!.GetValue(response);
        var features = response.GetType().GetProperty("features")!.GetValue(response) as System.Collections.IEnumerable;

        Assert.Equal("FeatureCollection", typeProp);
        Assert.NotNull(features);
        Assert.Empty(features.Cast<object>());
    }
}

