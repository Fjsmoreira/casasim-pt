using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/properties")]
public sealed class PropertiesController : ControllerBase
{
    private readonly AppDbContext _db;

    public PropertiesController(AppDbContext db) => _db = db;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<Listing>>> Search(
        [FromQuery] string? city,
        [FromQuery] ListingPropertyType? type,
        [FromQuery] ListingPriceType? priceType,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int? minBedrooms,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Listings.AsQueryable();

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

        query = query.Include(l => l.Agency)
                     .Include(l => l.Location)
                     .Include(l => l.Images)
                     .Include(l => l.Features)
                     .OrderByDescending(l => l.UpdatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Listing>> GetById(Guid id)
    {
        var listing = await _db.Listings
            .Include(l => l.Agency)
            .Include(l => l.Location)
            .Include(l => l.Images.OrderBy(i => i.SortOrder))
            .Include(l => l.Features)
            .FirstOrDefaultAsync(l => l.Id == id);

        if (listing is null) return NotFound();
        return Ok(listing);
    }
}
