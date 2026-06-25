using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Core.Models;
using CasaSim.Scraper.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using NetTopologySuite.Geometries;

namespace CasaSim.Api.Tests;

public sealed class ListingUpsertServiceTests
{
    private static readonly DateTimeOffset SeedTime = new(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);

    private static AppDbContext CreateDb(string? dbName = null)
    {
        dbName ??= "UpsertTests_" + Guid.NewGuid();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var db = new AppDbContext(options);

        // Seed the agency — same key as in AppDbContext seed (only if needed)
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

    private static Property MakeProperty(string externalId = "122591135-5")
    {
        return new Property
        {
            ExternalId = externalId,
            SourceAgency = "Remax",
            Title = "Moradia T3 à venda em Abiul, Pombal",
            Description = "Uma bela moradia com 3 quartos.",
            Price = 185000m,
            Currency = "EUR",
            Type = PropertyType.House,
            Transaction = TransactionType.Sale,
            Address = "Rua Principal, Nº12",
            City = "Pombal",
            District = "Leiria",
            PostalCode = "3100-000",
            AreaM2 = 150.0,
            LandAreaM2 = 500.0,
            Bedrooms = 3,
            Bathrooms = 2,
            ParkingSpots = 2,
            YearBuilt = 2005,
            EnergyClass = "C",
            Images = ["https://i.maxwork.pt/ds-l/img1.jpg", "https://i.maxwork.pt/ds-l/img2.jpg"],
            ListingUrl = "https://www.remax.pt/comprar/moradia/pombal/122591135-5",
            PublishedAt = new DateTime(2026, 6, 8, 17, 15, 34, DateTimeKind.Utc),
            Status = PropertyStatus.Active,
            Location = new Point(-8.6283, 39.9167) { SRID = 4326 },
        };
    }

    private static ListingUpsertService CreateService(AppDbContext db)
    {
        return new ListingUpsertService(db, new Mock<ILogger<ListingUpsertService>>().Object);
    }

    // ──────────────────────────────────────────────────────────
    //  CREATE
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_Creates_New_Listing()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.UpsertAsync(MakeProperty(), "remax-pombal");
        await db.SaveChangesAsync();

        Assert.Equal(ListingUpsertAction.Created, result.Action);

        var listing = await db.Listings
            .Include(l => l.Agency)
            .Include(l => l.Images)
            .FirstOrDefaultAsync(l => l.ExternalId == "122591135-5");

        Assert.NotNull(listing);
        Assert.Equal("remax-pombal", listing.Agency?.Slug);
        Assert.Equal("Moradia T3 à venda em Abiul, Pombal", listing.Title);
        Assert.Equal(185000m, listing.Price);
        Assert.Equal(3, listing.Bedrooms);
        Assert.Equal(150m, listing.AreaM2);
        Assert.Equal(500m, listing.LandAreaM2);
        Assert.Equal(ListingPropertyType.House, listing.PropertyType);
        Assert.Equal(ListingPriceType.Sale, listing.PriceType);
        Assert.Equal(ListingStatus.Active, listing.Status);
        Assert.Equal("C", listing.EnergyClass);
        Assert.Equal(new DateTimeOffset(2026, 6, 8, 17, 15, 34, TimeSpan.Zero), listing.PublishedAt);

        // Timestamps — compare within 1 second tolerance (in-memory DB precision)
        Assert.Equal(listing.FirstSeenAt, listing.LastSeenAt, TimeSpan.FromMilliseconds(100));

        // Images
        Assert.Equal(2, listing.Images.Count);

        var primary = listing.Images.First(i => i.IsPrimary);
        Assert.Equal("https://i.maxwork.pt/ds-l/img1.jpg", primary.Url);
    }

    // ──────────────────────────────────────────────────────────
    //  UPDATE
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_Skips_Unchanged_Existing_Listing_And_Refreshes_LastSeen()
    {
        var db = CreateDb();
        var svc = CreateService(db);
        var scrapeLogId = Guid.NewGuid();
        db.ScrapeLogs.Add(new ScrapeLog
        {
            Id = scrapeLogId,
            SourceName = "Remax",
            Status = ScrapeStatus.Started,
            StartedAt = SeedTime,
            CreatedAt = SeedTime,
            UpdatedAt = SeedTime,
            FirstSeenAt = SeedTime,
            LastSeenAt = SeedTime,
        });
        await db.SaveChangesAsync();

        // Create first
        var result1 = await svc.UpsertAsync(MakeProperty(), "remax-pombal", scrapeLogId);
        await db.SaveChangesAsync();
        Assert.Equal(ListingUpsertAction.Created, result1.Action);

        var listingBefore = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == result1.ListingId);
        await Task.Delay(10);

        // Second identical call detects existing -> returns Skipped action.
        var result2 = await svc.UpsertAsync(MakeProperty(), "remax-pombal", scrapeLogId);
        await db.SaveChangesAsync();
        Assert.Equal(ListingUpsertAction.Skipped, result2.Action);
        Assert.Equal(result1.ListingId, result2.ListingId);

        var listingAfter = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == result1.ListingId);
        Assert.Equal(listingBefore.UpdatedAt, listingAfter.UpdatedAt);
        Assert.True(listingAfter.LastSeenAt >= listingBefore.LastSeenAt);

        var changes = await db.ScrapeListingChanges
            .Where(c => c.ScrapeLogId == scrapeLogId)
            .ToListAsync();
        Assert.Single(changes);
        Assert.Equal(ScrapeListingChangeAction.Created, changes[0].Action);
    }

    [Fact]
    public async Task UpsertAsync_Updates_Existing_Listing_When_Field_Changes()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        var result1 = await svc.UpsertAsync(MakeProperty(), "remax-pombal");
        await db.SaveChangesAsync();

        var changed = MakeProperty();
        changed.Price = 175000m;
        var result2 = await svc.UpsertAsync(changed, "remax-pombal");
        await db.SaveChangesAsync();

        Assert.Equal(ListingUpsertAction.Updated, result2.Action);
        Assert.Equal(result1.ListingId, result2.ListingId);
        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == result1.ListingId);
        Assert.Equal(175000m, listing.Price);
    }

    [Fact]
    public async Task UpsertAsync_Maps_Zero_Price_To_Null()
    {
        var db = CreateDb();
        var svc = CreateService(db);
        var property = MakeProperty();
        property.Price = 0m;

        var result = await svc.UpsertAsync(property, "remax-pombal");
        await db.SaveChangesAsync();

        Assert.Equal(ListingUpsertAction.Created, result.Action);
        var listing = await db.Listings.AsNoTracking().SingleAsync(l => l.Id == result.ListingId);
        Assert.Null(listing.Price);
    }

    // ──────────────────────────────────────────────────────────
    //  UPSERT — batch
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertBatchAsync_Bulk_Create()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        var props = new List<Property>
        {
            MakeProperty("listing-001"),
            MakeProperty("listing-002"),
            MakeProperty("listing-003"),
        };

        var result = await svc.UpsertBatchAsync(props, "remax-pombal");

        Assert.Equal(3, result.Created);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);

        var count = await db.Listings.CountAsync();
        Assert.Equal(3, count);
    }

    // ──────────────────────────────────────────────────────────
    //  UNKNOWN AGENCY
    // ──────────────────────────────────────────────────────────

    [Fact]
    public async Task UpsertAsync_UnknownAgency_Skips()
    {
        var db = CreateDb();
        var svc = CreateService(db);

        var result = await svc.UpsertAsync(MakeProperty(), "unknown-agency");
        await db.SaveChangesAsync();

        Assert.Equal(ListingUpsertAction.Skipped, result.Action);
        Assert.Empty(db.Listings);
    }
}
