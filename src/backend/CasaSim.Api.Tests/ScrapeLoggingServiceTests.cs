using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Scraper.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CasaSim.Api.Tests;

public sealed class ScrapeLoggingServiceTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateDb(string? dbName = null)
    {
        dbName ??= "ScrapeLogTest_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        if (!db.Agencies.Any())
        {
            db.Agencies.Add(new Agency
            {
                Id = Guid.Parse("a1000000-0000-0000-0000-000000000001"),
                Name = "Remax Pombal",
                Slug = "remax-pombal",
                WebsiteUrl = "https://www.remax.pt",
                IsActive = true,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
            });
            db.SaveChanges();
        }
        return db;
    }

    private static ScrapeLoggingService CreateService(AppDbContext db)
    {
        var logger = new Mock<ILogger<ScrapeLoggingService>>();
        return new ScrapeLoggingService(db, logger.Object);
    }

    [Fact]
    public async Task StartLog_CreatesRowWithStartedStatus()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var logId = await service.StartLogAsync(
            "Remax",
            sourceUrl: "https://www.remax.pt",
            agencyId: Guid.Parse("a1000000-0000-0000-0000-000000000001"));

        var log = await db.ScrapeLogs.FindAsync(logId);
        Assert.NotNull(log);
        Assert.Equal("Remax", log.SourceName);
        Assert.Equal("https://www.remax.pt", log.SourceUrl);
        Assert.Equal(ScrapeStatus.Started, log.Status);
        Assert.Null(log.CompletedAt);
        Assert.Equal(0, log.ListingsFound);
    }

    [Fact]
    public async Task CompleteLog_SetsSucceededStatusAndCounts()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var logId = await service.StartLogAsync("Remax", null, null);
        await service.CompleteLogAsync(logId,
            listingsFound: 10,
            listingsCreated: 5,
            listingsUpdated: 3,
            listingsRemoved: 1);

        var log = await db.ScrapeLogs.FindAsync(logId);
        Assert.NotNull(log);
        Assert.Equal(ScrapeStatus.Succeeded, log.Status);
        Assert.NotNull(log.CompletedAt);
        Assert.Equal(10, log.ListingsFound);
        Assert.Equal(5, log.ListingsCreated);
        Assert.Equal(3, log.ListingsUpdated);
        Assert.Equal(1, log.ListingsRemoved);
    }

    [Fact]
    public async Task FailLog_SetsFailedStatusAndErrorMessage()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var logId = await service.StartLogAsync("Remax", null, null);
        await service.FailLogAsync(logId,
            errorMessage: "HTTP 500 from upstream",
            errorDetails: "System.Net.Http.HttpRequestException: ...");

        var log = await db.ScrapeLogs.FindAsync(logId);
        Assert.NotNull(log);
        Assert.Equal(ScrapeStatus.Failed, log.Status);
        Assert.NotNull(log.CompletedAt);
        Assert.Equal("HTTP 500 from upstream", log.ErrorMessage);
        Assert.Equal("System.Net.Http.HttpRequestException: ...", log.ErrorDetails);
    }

    [Fact]
    public async Task ScrapeLifecycle_StartToComplete_RoundTrip()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        // Start
        var agencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");
        var logId = await service.StartLogAsync("Remax", "https://www.remax.pt", agencyId);

        var started = await db.ScrapeLogs.FindAsync(logId);
        Assert.NotNull(started);
        Assert.Equal(ScrapeStatus.Started, started.Status);
        Assert.Equal(agencyId, started.AgencyId);

        // Complete
        await service.CompleteLogAsync(logId,
            listingsFound: 25,
            listingsCreated: 10,
            listingsUpdated: 12,
            listingsRemoved: 3);

        var completed = await db.ScrapeLogs.FindAsync(logId);
        Assert.NotNull(completed);
        Assert.Equal(ScrapeStatus.Succeeded, completed.Status);
        Assert.NotNull(completed.CompletedAt);
        Assert.True(completed.CompletedAt > completed.StartedAt);
        Assert.Equal(25, completed.ListingsFound);
        Assert.Equal(10, completed.ListingsCreated);
        Assert.Equal(12, completed.ListingsUpdated);
        Assert.Equal(3, completed.ListingsRemoved);
    }

    [Fact]
    public async Task ResolveAgencyId_ReturnsIdForKnownSlug()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var id = await service.ResolveAgencyIdAsync("remax-pombal");

        Assert.NotNull(id);
        Assert.Equal(Guid.Parse("a1000000-0000-0000-0000-000000000001"), id.Value);
    }

    [Fact]
    public async Task ResolveAgencyId_ReturnsNullForUnknownSlug()
    {
        using var db = CreateDb();
        var service = CreateService(db);

        var id = await service.ResolveAgencyIdAsync("non-existent-agency");

        Assert.Null(id);
    }
}
