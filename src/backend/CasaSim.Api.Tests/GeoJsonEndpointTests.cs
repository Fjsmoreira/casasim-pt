using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Api.Services;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using NetTopologySuite.Geometries;

namespace CasaSim.Api.Tests;

public class GeoJsonEndpointTests
{
    // ===================== BOUNDS VALIDATION =====================
    //
    // The controller validates bounds BEFORE any database interaction,
    // so we create a real AppDbContext with InMemory (no real DB needed)
    // and a mock IListingQueryService (not touched by validation).

    [Theory]
    [InlineData(null, 8.5, 40.0, 9.0)]   // missing swLat
    [InlineData(39.5, null, 40.0, 9.0)]  // missing swLng
    [InlineData(39.5, 8.5, null, 9.0)]   // missing neLat
    [InlineData(39.5, 8.5, 40.0, null)]  // missing neLng
    public async Task GetGeoJson_MissingBoundParam_Returns400(
        double? swLat, double? swLng, double? neLat, double? neLng)
    {
        var ctrl = CreateController();
        var result = await ctrl.GetGeoJson(
            swLat: swLat, swLng: swLng, neLat: neLat, neLng: neLng,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(-91.0, 8.5, -89.0, 9.0)]   // swLat < -90
    [InlineData(91.0, 8.5, 92.0, 9.0)]     // both out of range
    [InlineData(39.0, 8.5, 95.0, 9.0)]     // neLat > 90
    [InlineData(-95.0, 8.5, 40.0, 9.0)]    // swLat < -90
    public async Task GetGeoJson_LatitudeOutOfRange_Returns400(
        double swLat, double swLng, double neLat, double neLng)
    {
        var ctrl = CreateController();
        var result = await ctrl.GetGeoJson(
            swLat: swLat, swLng: swLng, neLat: neLat, neLng: neLng,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Theory]
    [InlineData(39.0, -181.0, 40.0, -170.0)] // swLng < -180
    [InlineData(39.0, 8.5, 40.0, 181.0)]     // neLng > 180
    [InlineData(39.0, -200.0, 40.0, 10.0)]   // swLng < -180
    public async Task GetGeoJson_LongitudeOutOfRange_Returns400(
        double swLat, double swLng, double neLat, double neLng)
    {
        var ctrl = CreateController();
        var result = await ctrl.GetGeoJson(
            swLat: swLat, swLng: swLng, neLat: neLat, neLng: neLng,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetGeoJson_SwLatGteNeLat_Returns400()
    {
        var ctrl = CreateController();
        var result = await ctrl.GetGeoJson(
            swLat: 40.0, swLng: 8.5, neLat: 39.0, neLng: 9.0,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task GetGeoJson_SwLngGteNeLng_Returns400()
    {
        var ctrl = CreateController();
        var result = await ctrl.GetGeoJson(
            swLat: 39.0, swLng: 9.0, neLat: 40.0, neLng: 8.5,
            city: null, type: null, priceType: null,
            minPrice: null, maxPrice: null, minBedrooms: null);
        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    // ===================== UTILITY =====================

    private static ListingsController CreateController()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("GeoJsonTests_" + Guid.NewGuid())
            .Options;
        var db = new AppDbContext(options);
        var svc = new Mock<IListingQueryService>().Object;
        return new ListingsController(db, svc);
    }
}
