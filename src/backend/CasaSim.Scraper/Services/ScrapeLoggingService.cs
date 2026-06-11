using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CasaSim.Scraper.Services;

/// <summary>
/// Central service for creating and updating scrape run logs in the
/// ScrapeLogs table.  Every scrape run gets one row tracking its
/// lifecycle: started → succeeded / failed / partially-succeeded,
/// along with counts and error details.
/// </summary>
public sealed class ScrapeLoggingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<ScrapeLoggingService> _logger;

    public ScrapeLoggingService(AppDbContext db, ILogger<ScrapeLoggingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>
    /// Create a new ScrapeLog row with <see cref="ScrapeStatus.Started"/>.
    /// Returns the log id so the caller can complete or fail it later.
    /// </summary>
    public async Task<Guid> StartLogAsync(
        string sourceName,
        string? sourceUrl,
        Guid? agencyId,
        CancellationToken ct = default)
    {
        var log = new ScrapeLog
        {
            Id = Guid.NewGuid(),
            AgencyId = agencyId,
            SourceName = sourceName,
            SourceUrl = sourceUrl,
            Status = ScrapeStatus.Started,
            StartedAt = DateTimeOffset.UtcNow,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            FirstSeenAt = DateTimeOffset.UtcNow,
            LastSeenAt = DateTimeOffset.UtcNow,
        };

        _db.ScrapeLogs.Add(log);
        await _db.SaveChangesAsync(ct);

        _logger.LogDebug("Created scrape log {LogId} for '{Source}'", log.Id, sourceName);
        return log.Id;
    }

    /// <summary>
    /// Mark a previously started scrape log as succeeded, recording
    /// the aggregate counts from the scrape+upsert cycle.
    /// </summary>
    public async Task CompleteLogAsync(
        Guid logId,
        int listingsFound,
        int listingsCreated,
        int listingsUpdated,
        int listingsRemoved,
        CancellationToken ct = default)
    {
        var log = await _db.ScrapeLogs.FindAsync(new object[] { logId }, ct);
        if (log is null)
        {
            _logger.LogWarning("Scrape log {LogId} not found for completion", logId);
            return;
        }

        log.Status = ScrapeStatus.Succeeded;
        log.CompletedAt = DateTimeOffset.UtcNow;
        log.ListingsFound = listingsFound;
        log.ListingsCreated = listingsCreated;
        log.ListingsUpdated = listingsUpdated;
        log.ListingsRemoved = listingsRemoved;
        log.UpdatedAt = DateTimeOffset.UtcNow;
        log.LastSeenAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "Scrape log {LogId} completed: found={Found} created={Created} updated={Updated} removed={Removed}",
            logId, listingsFound, listingsCreated, listingsUpdated, listingsRemoved);
    }

    /// <summary>
    /// Mark a scrape log as failed, recording the error.
    /// </summary>
    public async Task FailLogAsync(
        Guid logId,
        string errorMessage,
        string? errorDetails = null,
        CancellationToken ct = default)
    {
        var log = await _db.ScrapeLogs.FindAsync(new object[] { logId }, ct);
        if (log is null)
        {
            _logger.LogWarning("Scrape log {LogId} not found for failure", logId);
            return;
        }

        log.Status = ScrapeStatus.Failed;
        log.CompletedAt = DateTimeOffset.UtcNow;
        log.ErrorMessage = errorMessage;
        log.ErrorDetails = errorDetails;
        log.UpdatedAt = DateTimeOffset.UtcNow;
        log.LastSeenAt = DateTimeOffset.UtcNow;

        await _db.SaveChangesAsync(ct);

        _logger.LogError("Scrape log {LogId} failed: {Message}", logId, errorMessage);
    }

    /// <summary>
    /// Resolve the Agency entity id from a slug, or null if not found.
    /// </summary>
    public async Task<Guid?> ResolveAgencyIdAsync(string slug, CancellationToken ct = default)
    {
        var agency = await _db.Agencies
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Slug == slug, ct);
        return agency?.Id;
    }
}
