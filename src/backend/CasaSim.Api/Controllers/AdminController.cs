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

    /// <summary>
    /// Scraper status: latest run per source, recent errors, overall stats.
    /// </summary>
    [HttpGet("scraper-status")]
    public async Task<ActionResult<object>> GetScraperStatus(CancellationToken ct)
    {
        // Latest run per source
        var latestRuns = await _db.ScrapeLogs
            .GroupBy(sl => sl.SourceName)
            .Select(g => g.OrderByDescending(sl => sl.StartedAt).First())
            .ToListAsync(ct);

        // Count runs by status
        var runCounts = await _db.ScrapeLogs
            .GroupBy(sl => sl.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync(ct);

        // Recent errors (last 5 failed runs)
        var recentErrors = await _db.ScrapeLogs
            .Where(sl => sl.Status == Core.Data.Entities.ScrapeStatus.Failed
                      || sl.Status == Core.Data.Entities.ScrapeStatus.PartiallySucceeded)
            .OrderByDescending(sl => sl.StartedAt)
            .Take(5)
            .Select(sl => new
            {
                sl.SourceName,
                sl.StartedAt,
                sl.ErrorMessage,
                sl.ErrorDetails,
            })
            .ToListAsync(ct);

        // Overall last run time
        var lastRunOverall = await _db.ScrapeLogs
            .OrderByDescending(sl => sl.StartedAt)
            .Select(sl => sl.StartedAt)
            .FirstOrDefaultAsync(ct);

        return Ok(new
        {
            sources = latestRuns.Select(sl => new
            {
                sl.SourceName,
                status = sl.Status.ToString(),
                sl.StartedAt,
                sl.CompletedAt,
                sl.ListingsFound,
                sl.ListingsCreated,
                sl.ListingsUpdated,
                sl.ListingsRemoved,
                sl.ErrorMessage,
            }),
            runCounts = runCounts.ToDictionary(k => k.Status.ToString()!, v => v.Count),
            recentErrors,
            lastRunOverall = lastRunOverall == default ? null : lastRunOverall,
        });
    }
}
