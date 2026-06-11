using CasaSim.Api;
using CasaSim.Core.Models;
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
    public async Task<ActionResult<IReadOnlyList<Property>>> Search(
        [FromQuery] string? city,
        [FromQuery] PropertyType? type,
        [FromQuery] TransactionType? transaction,
        [FromQuery] decimal? minPrice,
        [FromQuery] decimal? maxPrice,
        [FromQuery] int? minBedrooms,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        var query = _db.Properties.AsQueryable();

        if (!string.IsNullOrEmpty(city))
            query = query.Where(p => p.City!.Contains(city));
        if (type.HasValue)
            query = query.Where(p => p.Type == type.Value);
        if (transaction.HasValue)
            query = query.Where(p => p.Transaction == transaction.Value);
        if (minPrice.HasValue)
            query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue)
            query = query.Where(p => p.Price <= maxPrice.Value);
        if (minBedrooms.HasValue)
            query = query.Where(p => p.Bedrooms >= minBedrooms.Value);

        query = query.OrderByDescending(p => p.UpdatedAt);

        var total = await query.CountAsync();
        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        Response.Headers["X-Total-Count"] = total.ToString();

        return Ok(new { items, total, page, pageSize });
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<Property>> GetById(Guid id)
    {
        var property = await _db.Properties.FindAsync(id);
        if (property is null) return NotFound();
        return Ok(property);
    }
}
