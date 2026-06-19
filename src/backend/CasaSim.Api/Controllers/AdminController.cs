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
            lastRunOverall = lastRunOverall == default ? default(DateTimeOffset?) : lastRunOverall,
        });
    }

    [HttpGet("scraper-sources")]
    public async Task<ActionResult<object>> GetScraperSources(CancellationToken ct)
    {
        var sources = await _db.ScraperSources
            .AsNoTracking()
            .Include(s => s.Agency)
            .OrderBy(s => s.Name)
            .ToListAsync(ct);

        var latestRuns = await _db.ScrapeLogs
            .AsNoTracking()
            .GroupBy(sl => sl.SourceName)
            .Select(g => g.OrderByDescending(sl => sl.StartedAt).First())
            .ToListAsync(ct);

        var latestBySource = latestRuns.ToDictionary(sl => sl.SourceName, StringComparer.OrdinalIgnoreCase);

        return Ok(sources.Select(s =>
        {
            latestBySource.TryGetValue(s.ScraperKey, out var latest);
            return new
            {
                s.Id,
                s.Name,
                s.ScraperKey,
                s.AgencySlug,
                agencyName = s.Agency != null ? s.Agency.Name : null,
                s.SourceUrl,
                s.TargetDescription,
                s.Enabled,
                interval = s.Interval.ToString(),
                s.ManualRunRequestedAt,
                s.UpdatedAt,
                latestRun = latest is null ? null : new
                {
                    latest.Id,
                    status = latest.Status.ToString(),
                    latest.StartedAt,
                    latest.CompletedAt,
                    latest.ListingsFound,
                    latest.ListingsCreated,
                    latest.ListingsUpdated,
                    latest.ListingsRemoved,
                    latest.ErrorMessage,
                },
            };
        }));
    }

    [HttpPost("scraper-sources/{id:guid}/run")]
    public async Task<ActionResult<object>> RequestScraperRun(Guid id, CancellationToken ct)
    {
        var source = await _db.ScraperSources.FindAsync(new object[] { id }, ct);
        if (source is null)
            return NotFound();

        if (!source.Enabled)
            return BadRequest(new { message = "Enable the scraper source before requesting a run." });

        if (source.ManualRunRequestedAt is null)
        {
            source.ManualRunRequestedAt = DateTimeOffset.UtcNow;
            source.UpdatedAt = source.ManualRunRequestedAt.Value;
            await _db.SaveChangesAsync(ct);
        }

        return Accepted(new { source.Id, source.Name, manualRunRequestedAt = source.ManualRunRequestedAt });
    }

    [HttpPatch("scraper-sources/{id:guid}")]
    public async Task<ActionResult<object>> UpdateScraperSource(
        Guid id,
        [FromBody] UpdateScraperSourceRequest request,
        CancellationToken ct)
    {
        var source = await _db.ScraperSources.FindAsync(new object[] { id }, ct);
        if (source is null)
            return NotFound();

        source.Enabled = request.Enabled;
        source.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return Ok(new
        {
            source.Id,
            source.Name,
            source.ScraperKey,
            source.AgencySlug,
            source.SourceUrl,
            source.TargetDescription,
            source.Enabled,
            interval = source.Interval.ToString(),
            source.UpdatedAt,
        });
    }

    [HttpGet("scrape-runs")]
    public async Task<ActionResult<PagedResult<object>>> GetScrapeRuns(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? sourceName = null,
        [FromQuery] string? status = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        IQueryable<ScrapeLog> query = _db.ScrapeLogs
            .AsNoTracking()
            .Include(sl => sl.Agency);

        if (!string.IsNullOrWhiteSpace(sourceName))
            query = query.Where(sl => sl.SourceName == sourceName);

        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<ScrapeStatus>(status, ignoreCase: true, out var statusFilter))
        {
            query = query.Where(sl => sl.Status == statusFilter);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(sl => sl.StartedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(sl => new
            {
                sl.Id,
                sl.SourceName,
                sl.SourceUrl,
                agencyName = sl.Agency != null ? sl.Agency.Name : null,
                agencySlug = sl.Agency != null ? sl.Agency.Slug : null,
                status = sl.Status.ToString(),
                sl.StartedAt,
                sl.CompletedAt,
                sl.ListingsFound,
                sl.ListingsCreated,
                sl.ListingsUpdated,
                sl.ListingsRemoved,
                sl.ErrorMessage,
            })
            .ToListAsync(ct);

        var items = rows.Select(sl => (object)new
        {
            sl.Id,
            sl.SourceName,
            sl.SourceUrl,
            sl.agencyName,
            sl.agencySlug,
            sl.status,
            sl.StartedAt,
            sl.CompletedAt,
            durationSeconds = sl.CompletedAt == null
                ? null
                : (double?)(sl.CompletedAt.Value - sl.StartedAt).TotalSeconds,
            sl.ListingsFound,
            sl.ListingsCreated,
            sl.ListingsUpdated,
            sl.ListingsRemoved,
            sl.ErrorMessage,
        }).ToList();

        return Ok(new PagedResult<object>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        });
    }

    [HttpGet("scrape-runs/{id:guid}")]
    public async Task<ActionResult<object>> GetScrapeRun(Guid id, CancellationToken ct)
    {
        var run = await _db.ScrapeLogs
            .AsNoTracking()
            .Include(sl => sl.Agency)
            .FirstOrDefaultAsync(sl => sl.Id == id, ct);

        if (run is null)
            return NotFound();

        var source = await _db.ScraperSources
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.ScraperKey == run.SourceName, ct);

        return Ok(new
        {
            run.Id,
            run.SourceName,
            run.SourceUrl,
            sourceTargetDescription = source?.TargetDescription,
            agencyName = run.Agency != null ? run.Agency.Name : null,
            agencySlug = run.Agency != null ? run.Agency.Slug : null,
            status = run.Status.ToString(),
            run.StartedAt,
            run.CompletedAt,
            durationSeconds = run.CompletedAt == null
                ? null
                : (double?)(run.CompletedAt.Value - run.StartedAt).TotalSeconds,
            run.ListingsFound,
            run.ListingsCreated,
            run.ListingsUpdated,
            run.ListingsRemoved,
            run.ErrorMessage,
            run.ErrorDetails,
        });
    }

    [HttpGet("scrape-runs/{id:guid}/changes")]
    public async Task<ActionResult<PagedResult<object>>> GetScrapeRunChanges(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? action = null,
        CancellationToken ct = default)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 200);

        var runExists = await _db.ScrapeLogs.AnyAsync(sl => sl.Id == id, ct);
        if (!runExists)
            return NotFound();

        IQueryable<ScrapeListingChange> query = _db.ScrapeListingChanges
            .AsNoTracking()
            .Where(c => c.ScrapeLogId == id);

        if (!string.IsNullOrWhiteSpace(action) &&
            Enum.TryParse<ScrapeListingChangeAction>(action, ignoreCase: true, out var actionFilter))
        {
            query = query.Where(c => c.Action == actionFilter);
        }

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.ScrapeLogId,
                c.ListingId,
                action = c.Action.ToString(),
                c.AgencySlug,
                c.ExternalId,
                c.Title,
                c.SourceUrl,
                c.ChangeSummaryJson,
                c.CreatedAt,
            })
            .Cast<object>()
            .ToListAsync(ct);

        return Ok(new PagedResult<object>
        {
            Items = items,
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
        });
    }
}

public sealed class UpdateScraperSourceRequest
{
    public bool Enabled { get; init; }
}
