using CasaSim.Api.Auth;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/admin")]
[ServiceFilter(typeof(AdminAuthenticationFilter))]
public sealed class AdminController : ControllerBase
{
    private readonly AppDbContext _db;

    public AdminController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Dashboard summary: total listings, active, scraped.
    /// </summary>
    [HttpGet("dashboard")]
    public async Task<ActionResult<object>> GetDashboard(CancellationToken ct)
    {
        var total = await _db.Listings.CountAsync(ct);
        var active = await _db.Listings.CountAsync(l => l.Status == CasaSim.Core.Data.Entities.ListingStatus.Active, ct);
        var scrapedToday = await _db.Listings
            .CountAsync(l => l.CreatedAt >= DateTimeOffset.UtcNow.Date, ct);

        return Ok(new
        {
            totalListings = total,
            activeListings = active,
            scrapedToday,
        });
    }

    /// <summary>
    /// Health / ping endpoint for admin connectivity verification.
    /// </summary>
    [HttpGet("ping")]
    public ActionResult<object> Ping()
    {
        return Ok(new { status = "ok", timestamp = DateTime.UtcNow });
    }
}
