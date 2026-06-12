using CasaSim.Api.Models;
using CasaSim.Api.Services;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IListingQueryService _listingQuery;

    public ListingsController(AppDbContext db, IListingQueryService listingQuery)
    {
        _db = db;
        _listingQuery = listingQuery;
    }

    /// <summary>
    /// Search listings with filtering, sorting, and pagination.
    /// Returns a paged list of listing summaries.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> GetListings(
        [FromQuery] ListingSearchRequest request,
        CancellationToken ct)
    {
        var result = await _listingQuery.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a single listing by ID with full details (images, features, agency, location).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListingDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var listing = await _db.Listings
            .AsNoTracking()
            .Include(l => l.Agency)
            .Include(l => l.Location)
            .Include(l => l.Images.OrderBy(i => i.SortOrder))
            .Include(l => l.Features)
            .Where(l => l.Id == id)
            .Select(l => new ListingDetailDto
            {
                Id = l.Id,
                Title = l.Title,
                Price = l.Price,
                PriceType = l.PriceType.ToString(),
                Currency = l.Currency,
                PropertyType = l.PropertyType.ToString(),
                Status = l.Status.ToString(),
                City = l.City,
                Parish = l.Location != null ? l.Location.Parish : null,
                District = l.Location != null ? l.Location.District : null,
                Description = l.Description,
                Latitude = l.Location != null ? (double?)l.Location.Latitude : null,
                Longitude = l.Location != null ? (double?)l.Location.Longitude : null,
                Bedrooms = l.Bedrooms,
                Bathrooms = l.Bathrooms,
                AreaM2 = l.AreaM2,
                LandAreaM2 = l.LandAreaM2,
                Images = l.Images
                    .Select(i => i.Url)
                    .ToList(),
                PrimaryImage = l.Images
                    .Where(i => i.IsPrimary)
                    .Select(i => new ListingImageDto
                    {
                        Id = i.Id,
                        Url = i.Url,
                        ThumbnailUrl = i.ThumbnailUrl,
                        AltText = i.AltText,
                        IsPrimary = i.IsPrimary,
                        SortOrder = i.SortOrder,
                    })
                    .FirstOrDefault(),
                Features = l.Features
                    .Select(f => f.Name)
                    .ToList(),
                SourceUrl = l.SourceUrl,
                ExternalId = l.ExternalId,
                AgencyName = l.Agency != null ? l.Agency.Name : null,
                AgencyPhone = l.Agency != null ? l.Agency.ContactPhone : null,
                AgencyEmail = l.Agency != null ? l.Agency.ContactEmail : null,
                AgencyWebsiteUrl = l.Agency != null ? l.Agency.WebsiteUrl : null,
                PublishedAt = l.PublishedAt,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .FirstOrDefaultAsync(ct);

        if (listing is null)
            return NotFound(new { error = "Listing not found.", id });

        return Ok(listing);
    }

    /// <summary>
    /// Returns active listings within a bounding box as a GeoJSON FeatureCollection.
    /// Required query params: swLat, swLng, neLat, neLng.
    /// Optional filters: city, type, priceType, minPrice, maxPrice, minBedrooms.
    /// </summary>
    [HttpGet("geojson")]
    public async Task<ActionResult<object>> GetGeoJson(
        [FromQuery] double? swLat,
        [FromQuery] double? swLng,
        [FromQuery] double? neLat,
        [FromQuery] double? neLng,
        [FromQuery] string? city,
        [FromQuery] ListingPropertyType? type,
        [FromQuery] ListingPriceType? priceType,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int? minBedrooms)
    {
        // --- Require bounding box ---
        if (swLat is null || swLng is null || neLat is null || neLng is null)
            return BadRequest(new { error = "Bounds are required: swLat, swLng, neLat, neLng." });

        // --- Bounds validation ---
        if (!IsValidLat(swLat.Value) || !IsValidLat(neLat.Value))
            return BadRequest(new { error = "Latitude must be between -90 and 90." });
        if (!IsValidLng(swLng.Value) || !IsValidLng(neLng.Value))
            return BadRequest(new { error = "Longitude must be between -180 and 180." });
        if (swLat.Value >= neLat.Value)
            return BadRequest(new { error = "swLat must be less than neLat." });
        if (swLng.Value >= neLng.Value)
            return BadRequest(new { error = "swLng must be less than neLng." });

        // --- Build bounding box polygon (SRID 4326) ---
        var bbox = BoundingBoxPolygon(swLat.Value, swLng.Value, neLat.Value, neLng.Value);

        // --- Query ---
        var listings = await BuildQuery(bbox, city, type, priceType, minPrice, maxPrice, minBedrooms)
            .OrderByDescending(l => l.UpdatedAt)
            .Select(l => new GeoJsonRecord
            {
                Id = l.Id,
                Price = l.Price,
                PriceType = l.PriceType,
                PropertyType = l.PropertyType,
                Status = l.Status,
                Currency = l.Currency,
                City = l.City,
                Bedrooms = l.Bedrooms,
                CoordinateX = l.Location!.Coordinate!.X,
                CoordinateY = l.Location!.Coordinate!.Y,
                ThumbnailUrl = l.Images
                    .Where(i => i.IsPrimary)
                    .Select(i => i.ThumbnailUrl ?? i.Url)
                    .FirstOrDefault()
            })
            .ToListAsync();

        // --- Build FeatureCollection ---
        var features = listings.Select(MapToFeature).ToList();

        return Ok(new
        {
            type = "FeatureCollection",
            features
        });
    }

    // --- Private helpers ---

    private IQueryable<Listing> BuildQuery(
        Polygon bbox,
        string? city,
        ListingPropertyType? type,
        ListingPriceType? priceType,
        decimal? minPrice,
        decimal? maxPrice,
        int? minBedrooms)
    {
        var query = _db.Listings
            .AsNoTracking()
            .Where(l => l.Status == ListingStatus.Active)
            .Where(l => l.Location != null && l.Location.Coordinate != null)
            .Where(l => l.Location!.Coordinate!.Intersects(bbox));

        if (!string.IsNullOrEmpty(city))
            query = query.Where(l => l.City != null && l.City.Contains(city));
        if (type.HasValue)
            query = query.Where(l => l.PropertyType == type.Value);
        if (priceType.HasValue)
            query = query.Where(l => l.PriceType == priceType.Value);
        if (minPrice.HasValue)
            query = query.Where(l => l.Price >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(l => l.Price <= maxPrice.Value);
        if (minBedrooms.HasValue)
            query = query.Where(l => l.Bedrooms >= minBedrooms.Value);

        return query;
    }

    private static object MapToFeature(GeoJsonRecord r)
    {
        return new
        {
            type = "Feature",
            geometry = new
            {
                type = "Point",
                coordinates = new[] { r.CoordinateX, r.CoordinateY }
            },
            properties = new
            {
                id = r.Id,
                price = r.Price,
                price_type = r.PriceType.ToString(),
                currency = r.Currency,
                property_type = r.PropertyType.ToString(),
                status = r.Status.ToString(),
                city = r.City,
                bedrooms = r.Bedrooms,
                thumbnail = r.ThumbnailUrl
            }
        };
    }

    private static Polygon BoundingBoxPolygon(double swLat, double swLng, double neLat, double neLng)
    {
        return new Polygon(
            new LinearRing(
            [
                new Coordinate(swLng, swLat),
                new Coordinate(neLng, swLat),
                new Coordinate(neLng, neLat),
                new Coordinate(swLng, neLat),
                new Coordinate(swLng, swLat)
            ]),
            new GeometryFactory(new PrecisionModel(), 4326));
    }

    private static bool IsValidLat(double lat) => lat is >= -90 and <= 90;
    private static bool IsValidLng(double lng) => lng is >= -180 and <= 180;

    // --- Projection DTO (EF Core Select target) ---
    private sealed class GeoJsonRecord
    {
        public Guid Id { get; init; }
        public decimal? Price { get; init; }
        public ListingPriceType PriceType { get; init; }
        public ListingPropertyType PropertyType { get; init; }
        public ListingStatus Status { get; init; }
        public string Currency { get; init; } = "EUR";
        public string? City { get; init; }
        public int? Bedrooms { get; init; }
        public double CoordinateX { get; init; }
        public double CoordinateY { get; init; }
        public string? ThumbnailUrl { get; init; }
    }
}
