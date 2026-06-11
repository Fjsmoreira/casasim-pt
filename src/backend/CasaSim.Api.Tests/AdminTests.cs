using CasaSim.Api.Auth;
using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using Moq;
using Microsoft.Extensions.Primitives;

namespace CasaSim.Api.Tests;

public sealed class AdminAuthenticationFilterTests
{
    private static AuthorizationFilterContext CreateContext(
        string? apiKeyHeader = null,
        string? configuredKey = null)
    {
        var config = new Mock<Microsoft.Extensions.Configuration.IConfiguration>();
        config.Setup(c => c["AdminSettings:ApiKey"]).Returns(configuredKey);

        var filter = new AdminAuthenticationFilter(config.Object);

        var httpContext = new DefaultHttpContext();
        if (apiKeyHeader is not null)
        {
            httpContext.Request.Headers["X-Api-Key"] = apiKeyHeader;
        }

        var actionContext = new ActionContext
        {
            HttpContext = httpContext,
            RouteData = new Microsoft.AspNetCore.Routing.RouteData(),
            ActionDescriptor = new Microsoft.AspNetCore.Mvc.Controllers.ControllerActionDescriptor(),
        };

        var ctx = new AuthorizationFilterContext(actionContext, new List<IFilterMetadata>());

        filter.OnAuthorizationAsync(ctx).GetAwaiter().GetResult();

        return ctx;
    }

    [Fact]
    public void ValidApiKey_ReturnsNull_NoError()
    {
        var ctx = CreateContext(apiKeyHeader: "secret-key", configuredKey: "secret-key");
        Assert.Null(ctx.Result);
    }

    [Fact]
    public void MissingApiKey_ReturnsUnauthorized()
    {
        var ctx = CreateContext(apiKeyHeader: null, configuredKey: "secret-key");
        var result = Assert.IsType<UnauthorizedObjectResult>(ctx.Result);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void WrongApiKey_ReturnsUnauthorized()
    {
        var ctx = CreateContext(apiKeyHeader: "wrong-key", configuredKey: "secret-key");
        var result = Assert.IsType<UnauthorizedObjectResult>(ctx.Result);
        Assert.Equal(401, result.StatusCode);
    }

    [Fact]
    public void EmptyConfiguredKey_Returns500()
    {
        var ctx = CreateContext(apiKeyHeader: "any-key", configuredKey: "");
        var result = Assert.IsType<StatusCodeResult>(ctx.Result);
        Assert.Equal(500, result.StatusCode);
    }

    [Fact]
    public void NullConfiguredKey_Returns500()
    {
        var ctx = CreateContext(apiKeyHeader: "any-key", configuredKey: null);
        var result = Assert.IsType<StatusCodeResult>(ctx.Result);
        Assert.Equal(500, result.StatusCode);
    }
}

public sealed class AdminControllerTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    private static AppDbContext CreateDb(string? dbName = null)
    {
        dbName ??= "AdminTests_" + Guid.NewGuid();
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
            Id = AgencyId,
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            WebsiteUrl = "https://www.remax.pt",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });

        db.Listings.AddRange(
            new Listing
            {
                Id = Guid.NewGuid(),
                AgencyId = AgencyId,
                ExternalId = "adm-001",
                Title = "Active Apartment",
                Price = 85000m,
                PropertyType = ListingPropertyType.Apartment,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Active,
                City = "Pombal",
                Bedrooms = 2,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new Listing
            {
                Id = Guid.NewGuid(),
                AgencyId = AgencyId,
                ExternalId = "adm-002",
                Title = "Sold House",
                Price = 200000m,
                PropertyType = ListingPropertyType.House,
                PriceType = ListingPriceType.Sale,
                Status = ListingStatus.Sold,
                City = "Leiria",
                Bedrooms = 3,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            }
        );

        db.SaveChanges();
    }

    [Fact]
    public async Task GetDashboard_ReturnsCounts()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetDashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = ok.Value;
        var total = response.GetType().GetProperty("totalListings")!.GetValue(response);
        var active = response.GetType().GetProperty("activeListings")!.GetValue(response);

        Assert.Equal(2, total);
        Assert.Equal(1, active);
    }

    [Fact]
    public async Task GetListings_ReturnsPagedResult()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetListings(page: 1, pageSize: 10);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
        Assert.Equal(2, paged.Items.Count);
    }

    [Fact]
    public async Task GetListings_FilterByStatus()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetListings(status: "Sold");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Single(paged.Items);
        Assert.Equal("Sold", paged.Items[0].Status);
    }

    [Fact]
    public async Task GetListings_FilterByAgency()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetListings(agency: "remax-pombal");

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
        Assert.Equal("remax-pombal", paged.Items[0].AgencySlug);
    }

    [Fact]
    public async Task GetListings_FilterByExpected_Values()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetListings(page: 1, pageSize: 10, status: null, agency: null);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
        Assert.NotNull(paged.Items[0].Title);
        Assert.NotNull(paged.Items[0].AgencyName);
    }

    [Fact]
    public async Task GetListings_Pagination()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetListings(page: 1, pageSize: 1);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var paged = Assert.IsType<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Equal(2, paged.TotalCount);
        Assert.Single(paged.Items);
        Assert.Equal(1, paged.Page);
        Assert.Equal(2, paged.TotalPages);
    }

    [Fact]
    public async Task GetAgencies_ReturnsList()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = await ctrl.GetAgencies(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var agencies = ok.Value as System.Collections.IEnumerable;
        Assert.NotNull(agencies);
        var list = agencies.Cast<object>().ToList();
        Assert.Single(list);
        var name = list[0].GetType().GetProperty("Name")!.GetValue(list[0]);
        Assert.Equal("Remax Pombal", name);
    }

    [Fact]
    public void Ping_ReturnsOk()
    {
        var db = CreateDb();
        var ctrl = new AdminController(db);

        var result = ctrl.Ping();

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var status = ok.Value.GetType().GetProperty("status")!.GetValue(ok.Value);
        Assert.Equal("ok", status);
    }

    [Fact]
    public async Task GetDashboard_EmptyDatabase_ReturnsZeroes()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AdminEmpty_" + Guid.NewGuid())
            .Options;
        var db = new AppDbContext(options);
        var ctrl = new AdminController(db);

        var result = await ctrl.GetDashboard(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = ok.Value;
        var total = response.GetType().GetProperty("totalListings")!.GetValue(response);
        var active = response.GetType().GetProperty("activeListings")!.GetValue(response);
        Assert.Equal(0, total);
        Assert.Equal(0, active);
    }
}
