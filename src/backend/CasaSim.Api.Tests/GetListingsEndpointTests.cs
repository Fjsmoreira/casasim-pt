using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Api.Services;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;

namespace CasaSim.Api.Tests;

public sealed class GetListingsEndpointTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private static AppDbContext CreateDb(string? dbName = null)
    {
        dbName ??= "GetListings_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);
        if (!db.Agencies.Any())
        {
            db.Agencies.Add(new Agency
            {
                Id = AgencyId,
                Name = "Remax Pombal",
                Slug = "remax-pombal",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
            });

            db.Listings.AddRange(
                new Listing
                {
                    Id = Guid.NewGuid(),
                    AgencyId = AgencyId,
                    ExternalId = "flt-001",
                    Title = "T1 Studio Pombal",
                    Price = 75000m,
                    PropertyType = ListingPropertyType.Apartment,
                    PriceType = ListingPriceType.Sale,
                    Status = ListingStatus.Active,
                    City = "Pombal",
                    Bedrooms = 1,
                    AreaM2 = 40m,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    FirstSeenAt = SeedTime,
                    LastSeenAt = SeedTime,
                },
                new Listing
                {
                    Id = Guid.NewGuid(),
                    AgencyId = AgencyId,
                    ExternalId = "flt-002",
                    Title = "T3 Family House Abiul",
                    Price = 195000m,
                    PropertyType = ListingPropertyType.House,
                    PriceType = ListingPriceType.Sale,
                    Status = ListingStatus.Active,
                    City = "Abiul",
                    Bedrooms = 3,
                    AreaM2 = 160m,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    FirstSeenAt = SeedTime,
                    LastSeenAt = SeedTime,
                },
                new Listing
                {
                    Id = Guid.NewGuid(),
                    AgencyId = AgencyId,
                    ExternalId = "flt-003",
                    Title = "T2 Apartment for Rent Pombal",
                    Price = 750m,
                    PropertyType = ListingPropertyType.Apartment,
                    PriceType = ListingPriceType.Rent,
                    Status = ListingStatus.Active,
                    City = "Pombal",
                    Bedrooms = 2,
                    AreaM2 = 85m,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    FirstSeenAt = SeedTime,
                    LastSeenAt = SeedTime,
                },
                new Listing
                {
                    Id = Guid.NewGuid(),
                    AgencyId = AgencyId,
                    ExternalId = "flt-004",
                    Title = "T2 Apartment Sold Leiria",
                    Price = 120000m,
                    PropertyType = ListingPropertyType.Apartment,
                    PriceType = ListingPriceType.Sale,
                    Status = ListingStatus.Sold,
                    City = "Leiria",
                    Bedrooms = 2,
                    AreaM2 = 90m,
                    CreatedAt = SeedTime,
                    UpdatedAt = SeedTime,
                    FirstSeenAt = SeedTime,
                    LastSeenAt = SeedTime,
                }
            );
            db.SaveChanges();
        }
        return db;
    }

    // ── Default returns only active listings ──

    [Fact]
    public async Task SearchAsync_Default_ReturnsAllActiveListings()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);
        var request = new ListingSearchRequest();

        var result = await svc.SearchAsync(request);

        // Only active listings (sold one is excluded)
        Assert.Equal(3, result.TotalCount);
        Assert.Equal(3, result.Items.Count);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
    }

    // ── Filter by PriceType ──

    [Fact]
    public async Task SearchAsync_FilterByPriceType_Rent()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            PriceType = "Rent",
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(750m, result.Items[0].Price);
        Assert.Equal("Rent", result.Items[0].PriceType);
    }

    // ── Filter by PropertyType ──

    [Fact]
    public async Task SearchAsync_FilterByPropertyType_House()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            PropertyType = "House",
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("T3 Family House Abiul", result.Items[0].Title);
    }

    // ── Filter by MinPrice ──

    [Fact]
    public async Task SearchAsync_FilterByMinPrice()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        // Active listings >= 100000: T3 House (195000)
        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            MinPrice = 100000m,
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(195000m, result.Items[0].Price);
    }

    // ── Filter by MaxPrice ──

    [Fact]
    public async Task SearchAsync_FilterByMaxPrice()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        // Active listings <= 80000: T1 Studio (75000) + T2 Rent (750)
        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            MaxPrice = 80000m,
        });

        Assert.Equal(2, result.TotalCount);
    }

    // ── Filter by MinBedrooms ──

    [Fact]
    public async Task SearchAsync_FilterByMinBedrooms()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            MinBedrooms = 2,
        });

        // T3 House (3br) + T2 Rent (2br)
        Assert.Equal(2, result.TotalCount);
    }

    // ── Pagination ──

    [Fact]
    public async Task SearchAsync_Pagination_Page1()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            Page = 1,
            PageSize = 2,
        });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(2, result.Items.Count);
        Assert.Equal(1, result.Page);
    }

    [Fact]
    public async Task SearchAsync_Pagination_Page2()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            Page = 2,
            PageSize = 2,
        });

        Assert.Equal(3, result.TotalCount);
        Assert.Equal(1, result.Items.Count);
        Assert.Equal(2, result.Page);
    }

    // ── Sorting ──

    [Fact]
    public async Task SearchAsync_SortByPriceAsc()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            SortBy = "Price",
            SortDirection = "Asc",
        });

        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items[0].Price <= result.Items[1].Price);
        Assert.True(result.Items[1].Price <= result.Items[2].Price);
    }

    [Fact]
    public async Task SearchAsync_SortByPriceDesc()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            SortBy = "Price",
            SortDirection = "Desc",
        });

        Assert.Equal(3, result.Items.Count);
        Assert.True(result.Items[0].Price >= result.Items[1].Price);
        Assert.True(result.Items[1].Price >= result.Items[2].Price);
    }

    // ── Price range (min + max combined) ──

    [Fact]
    public async Task SearchAsync_FilterByPriceRange()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            MinPrice = 50000m,
            MaxPrice = 100000m,
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal(75000m, result.Items[0].Price);
    }

    // ── Filter by Agency ──

    [Fact]
    public async Task SearchAsync_FilterByAgencySlug()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            AgencySlug = "remax-pombal",
        });

        Assert.Equal(3, result.TotalCount);
    }

    // ── Combined filters ──

    [Fact]
    public async Task SearchAsync_CombinedFilters()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            PropertyType = "Apartment",
            PriceType = "Sale",
            MinBedrooms = 1,
        });

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("T1 Studio Pombal", result.Items[0].Title);
    }

    // ── No results ──

    [Fact]
    public async Task SearchAsync_NoResults_ReturnsEmpty()
    {
        var db = CreateDb();
        var svc = new ListingQueryService(db);

        var result = await svc.SearchAsync(new ListingSearchRequest
        {
            PropertyType = "Land",
        });

        Assert.Equal(0, result.TotalCount);
        Assert.Empty(result.Items);
    }

    // ── Controller integration ──

    [Fact]
    public async Task GetListingsController_CallsService_ReturnsOk()
    {
        var db = CreateDb();
        var svc = new Mock<IListingQueryService>();
        var expected = new PagedResult<ListingSummaryDto>
        {
            Items = Array.Empty<ListingSummaryDto>(),
            Page = 1,
            PageSize = 20,
            TotalCount = 0,
        };
        svc.Setup(s => s.SearchAsync(It.IsAny<ListingSearchRequest>(), It.IsAny<CancellationToken>()))
           .ReturnsAsync(expected);

        var ctrl = new ListingsController(db, svc.Object);
        var result = await ctrl.GetListings(new ListingSearchRequest(), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<ListingSummaryDto>>(ok.Value);
        Assert.Equal(expected.TotalCount, paged.TotalCount);
    }
}
