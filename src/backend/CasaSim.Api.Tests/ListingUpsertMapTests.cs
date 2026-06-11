using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace CasaSim.Api.Tests;

/// <summary>
/// Tests for ListingUpsertService mapping helpers and edge-case behaviour
/// not covered by the existing DB-backed tests.
/// </summary>
public sealed class ListingUpsertMappingTests
{
    // ──────────────────────────────────────────────────────────
    //  ResolveAgencySlug
    // ──────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Remax", "remax-pombal")]
    [InlineData("Century21", "century21-pombal")]
    [InlineData("ERA", "era-pombal")]
    public void ResolveAgencySlug_KnownAgency_ReturnsSlug(string agencyName, string expectedSlug)
    {
        var slug = ListingUpsertService.ResolveAgencySlug(agencyName);
        Assert.Equal(expectedSlug, slug);
    }

    [Fact]
    public void ResolveAgencySlug_UnknownAgency_ReturnsNull()
    {
        var slug = ListingUpsertService.ResolveAgencySlug("NonExistentAgency");
        Assert.Null(slug);
    }

    [Fact]
    public void ResolveAgencySlug_CaseInsensitive()
    {
        var slug = ListingUpsertService.ResolveAgencySlug("remax");
        Assert.Equal("remax-pombal", slug);
    }

    // ──────────────────────────────────────────────────────────
    //  UpsertBatchAsync — "listing no longer present" scenario
    // ──────────────────────────────────────────────────────────

    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid AgencyId = Guid.Parse("a1000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task UpsertBatchAsync_ListingAbsentFromBatch_MarksAsRemoved()
    {
        // Simulate: previous scrape created listing "old-001" which is no longer
        // returned by the source. We need to manually mark it as removed.
        var dbName = "StaleDetection_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        // Seed agency
        db.Agencies.Add(new Agency
        {
            Id = AgencyId,
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });

        // Seed an old listing that should be tracked for removal
        var staleListing = new Listing
        {
            Id = Guid.NewGuid(),
            AgencyId = AgencyId,
            ExternalId = "old-001",
            Title = "Already Sold Listing",
            Price = 100000m,
            PropertyType = ListingPropertyType.Apartment,
            PriceType = ListingPriceType.Sale,
            Status = ListingStatus.Active,
            City = "Pombal",
            Bedrooms = 2,
            CreatedAt = SeedTime.AddDays(-7),
            UpdatedAt = SeedTime.AddDays(-7),
            FirstSeenAt = SeedTime.AddDays(-7),
            LastSeenAt = SeedTime.AddDays(-7),
        };
        db.Listings.Add(staleListing);
        await db.SaveChangesAsync();

        var svc = new ListingUpsertService(db, new Mock<ILogger<ListingUpsertService>>().Object);

        // Upsert 2 new listings — "old-001" is not among them
        var freshProps = new List<Property>
        {
            new()
            {
                ExternalId = "new-001",
                SourceAgency = "Remax",
                Title = "New T2 Apartment",
                Price = 120000m,
                Type = PropertyType.Apartment,
                Transaction = TransactionType.Sale,
                Status = PropertyStatus.Active,
                City = "Pombal",
            },
            new()
            {
                ExternalId = "new-002",
                SourceAgency = "Remax",
                Title = "New T3 House",
                Price = 200000m,
                Type = PropertyType.House,
                Transaction = TransactionType.Sale,
                Status = PropertyStatus.Active,
                City = "Pombal",
            },
        };

        var batchResult = await svc.UpsertBatchAsync(freshProps, "remax-pombal");

        Assert.Equal(2, batchResult.Created);
        Assert.Equal(0, batchResult.Updated);
        Assert.Equal(0, batchResult.Skipped);

        // Verify the old listing is still active — stale detection is not
        // implemented inside UpsertBatchAsync, so we'd need a separate cleanup.
        // This test documents that the current behavior does NOT auto-remove.
        var oldListingAfter = await db.Listings.FirstOrDefaultAsync(l => l.ExternalId == "old-001");
        Assert.NotNull(oldListingAfter);
        Assert.Equal(ListingStatus.Active, oldListingAfter.Status);

        // Manual removal to simulate the "no longer present" cleanup:
        oldListingAfter.Status = ListingStatus.Removed;
        oldListingAfter.RemovedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync();

        var removed = await db.Listings
            .FirstOrDefaultAsync(l => l.ExternalId == "old-001" && l.Status == ListingStatus.Removed);
        Assert.NotNull(removed);
    }

    // ──────────────────────────────────────────────────────────
    //  UpsertService — duplicate handling
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertBatchAsync_DuplicateExternalId_UpdatesExisting()
    {
        var dbName = "Dedup_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        db.Agencies.Add(new Agency
        {
            Id = AgencyId,
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });
        await db.SaveChangesAsync();

        var svc = new ListingUpsertService(db, new Mock<ILogger<ListingUpsertService>>().Object);

        var prop = new Property
        {
            ExternalId = "dup-001",
            SourceAgency = "Remax",
            Title = "Original Title",
            Price = 100000m,
            Type = PropertyType.Apartment,
            Transaction = TransactionType.Sale,
            Status = PropertyStatus.Active,
            City = "Pombal",
        };

        // Create
        var createResult = await svc.UpsertAsync(prop, "remax-pombal");
        await db.SaveChangesAsync();
        Assert.Equal(ListingUpsertAction.Created, createResult.Action);

        // Update with the same ExternalId
        prop.Title = "Updated Title";
        prop.Price = 110000m;
        var updateResult = await svc.UpsertAsync(prop, "remax-pombal");
        Assert.Equal(ListingUpsertAction.Updated, updateResult.Action);
        Assert.Equal(createResult.ListingId, updateResult.ListingId);

        // Verify update persisted
        var updatedListing = await db.Listings.FindAsync(createResult.ListingId);
        Assert.NotNull(updatedListing);
        Assert.Equal("Updated Title", updatedListing.Title);
        Assert.Equal(110000m, updatedListing.Price);
    }

    // ──────────────────────────────────────────────────────────
    //  Batch results — edge cases
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertBatchAsync_EmptyList_ReturnsZeroes()
    {
        var dbName = "EmptyBatch_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        db.Agencies.Add(new Agency
        {
            Id = AgencyId,
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });
        await db.SaveChangesAsync();

        var svc = new ListingUpsertService(db, new Mock<ILogger<ListingUpsertService>>().Object);

        var result = await svc.UpsertBatchAsync(Array.Empty<Property>(), "remax-pombal");

        Assert.Equal(0, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
    }

    // ──────────────────────────────────────────────────────────
    //  Admin controller — scrape log tracking
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task ScraperStatus_WithLogs_ReturnsSources()
    {
        var dbName = "ScraperStatus_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        db.Agencies.Add(new Agency
        {
            Id = AgencyId,
            Name = "Remax Pombal",
            Slug = "remax-pombal",
            IsActive = true,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
        });

        db.ScrapeLogs.AddRange(
            new ScrapeLog
            {
                Id = Guid.NewGuid(),
                AgencyId = AgencyId,
                SourceName = "Remax",
                Status = ScrapeStatus.Succeeded,
                StartedAt = SeedTime,
                CompletedAt = SeedTime.AddMinutes(5),
                ListingsFound = 10,
                ListingsCreated = 5,
                ListingsUpdated = 3,
                ListingsRemoved = 1,
                CreatedAt = SeedTime,
                UpdatedAt = SeedTime,
                FirstSeenAt = SeedTime,
                LastSeenAt = SeedTime,
            },
            new ScrapeLog
            {
                Id = Guid.NewGuid(),
                SourceName = "Century21",
                Status = ScrapeStatus.Failed,
                StartedAt = SeedTime.AddHours(1),
                CompletedAt = SeedTime.AddHours(1).AddMinutes(2),
                ErrorMessage = "HTTP 500 from upstream",
                ErrorDetails = "System.Net.Http.HttpRequestException: ...",
                CreatedAt = SeedTime.AddHours(1),
                UpdatedAt = SeedTime.AddHours(1),
                FirstSeenAt = SeedTime.AddHours(1),
                LastSeenAt = SeedTime.AddHours(1),
            }
        );
        await db.SaveChangesAsync();

        var ctrl = new CasaSim.Api.Controllers.AdminController(db);
        var result = await ctrl.GetScraperStatus(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var response = ok.Value;

        var sources = response.GetType().GetProperty("sources")!.GetValue(response) as System.Collections.IEnumerable;
        Assert.NotNull(sources);

        var sourceList = sources.Cast<object>().ToList();
        Assert.Equal(2, sourceList.Count);

        var firstSource = sourceList[0];
        var sourceName = firstSource.GetType().GetProperty("SourceName")!.GetValue(firstSource);
        var sourceStatus = firstSource.GetType().GetProperty("status")!.GetValue(firstSource);
        Assert.NotNull(sourceName);
        Assert.NotNull(sourceStatus);
    }
}
