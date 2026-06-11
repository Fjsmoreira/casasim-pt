using CasaSim.Api.Auth;
using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
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
    /// Listings management table: thumbnail, title, price, source, status, last seen.
    /// Supports pagination and basic filters (status, agency, search).
    /// </summary>
    [HttpGet("listings")]
    public async Task<ActionResult<PagedResult<AdminListingDto>>> GetListings(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        [FromQuery] string? agency = null,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        IQueryable<Listing> query = _db.Listings
            .AsNoTracking()
            .Include(l => l.Agency)
            .Include(l => l.Images.Where(i => i.IsPrimary));

        // ── Filters ──
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ListingStatus>(status, ignoreCase: true, out var statusFilter))
        {
            query = query.Where(l => l.Status == statusFilter);
        }

        if (!string.IsNullOrWhiteSpace(agency))
        {
            query = query.Where(l => l.Agency != null && l.Agency.Slug == agency);
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(l =>
                EF.Functions.ILike(l.Title, $"%{search}%") ||
                (l.City != null && EF.Functions.ILike(l.City, $"%{search}%")));
        }

        // ── Total count ──
        var totalCount = await query.CountAsync(ct);

        // ── Sorting: most recently seen first ──
        query = query
            .OrderByDescending(l => l.LastSeenAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize);

        // ── Project ──
        var items = await query
            .Select(l => new AdminListingDto
            {
                Id = l.Id,
                Title = l.Title,
                Price = l.Price,
                PriceFormatted = l.Price != null ? $"€{l.Price:N0}" : null,
                Currency = l.Currency,
                PropertyType = l.PropertyType.ToString(),
                Status = l.Status.ToString(),
                City = l.City,
                Bedrooms = l.Bedrooms,
                AreaM2 = l.AreaM2,
                ThumbnailUrl = l.Images
                    .Where(i => i.IsPrimary)
                    .Select(i => i.ThumbnailUrl ?? i.Url)
                    .FirstOrDefault(),
                AgencyName = l.Agency != null ? l.Agency.Name : null,
                AgencySlug = l.Agency != null ? l.Agency.Slug : null,
                LastSeenAt = l.LastSeenAt,
                CreatedAt = l.CreatedAt,
                UpdatedAt = l.UpdatedAt,
            })
            .ToListAsync(ct);

        return Ok(new PagedResult<AdminListingDto>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount,
        });
    }

    /// <summary>
    /// Returns the list of agencies for admin filter dropdowns.
    /// </summary>
    [HttpGet("agencies")]
    public async Task<ActionResult> GetAgencies(CancellationToken ct)
    {
        var agencies = await _db.Agencies
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => new
            {
                a.Id,
                a.Name,
                a.Slug,
            })
            .ToListAsync(ct);

        return Ok(agencies);
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
