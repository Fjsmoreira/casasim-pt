using CasaSim.Api;
using CasaSim.Api.Models;
using CasaSim.Api.Services;
using CasaSim.Core.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Tests;

public sealed class ListingQueryServiceTests
{
    [Fact]
    public async Task SearchAsync_Filters_By_DealLabel()
    {
        await using var db = CreateDb();
        var agency = new Agency { Id = Guid.NewGuid(), Name = "Agency", Slug = "agency", IsActive = true };
        var good = MakeListing(agency.Id, "Good listing");
        var bad = MakeListing(agency.Id, "Bad listing");
        db.Agencies.Add(agency);
        db.Listings.AddRange(good, bad);
        db.ListingAiEnrichments.AddRange(
            MakeEnrichment(good.Id, "GoodDeal", 82m),
            MakeEnrichment(bad.Id, "BadDeal", 30m));
        await db.SaveChangesAsync();

        var result = await new ListingQueryService(db).SearchAsync(new ListingSearchRequest
        {
            DealLabel = "GoodDeal",
        });

        var item = Assert.Single(result.Items);
        Assert.Equal(good.Id, item.Id);
        Assert.Equal("GoodDeal", item.Ai?.DealLabel);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("ListingQueryTests_" + Guid.NewGuid())
            .Options;
        return new AppDbContext(options);
    }

    private static Listing MakeListing(Guid agencyId, string title) => new()
    {
        Id = Guid.NewGuid(),
        AgencyId = agencyId,
        ExternalId = Guid.NewGuid().ToString("N"),
        SourceUrl = "https://example.com",
        Title = title,
        Status = ListingStatus.Active,
        PropertyType = ListingPropertyType.House,
        PriceType = ListingPriceType.Sale,
        Currency = "EUR",
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        FirstSeenAt = DateTimeOffset.UtcNow,
        LastSeenAt = DateTimeOffset.UtcNow,
    };

    private static ListingAiEnrichment MakeEnrichment(Guid listingId, string dealLabel, decimal dealScore) => new()
    {
        Id = Guid.NewGuid(),
        ListingId = listingId,
        SourceHash = Guid.NewGuid().ToString("N"),
        GeneratedDescription = "Resumo AI",
        DealScore = dealScore,
        DealLabel = dealLabel,
        DealReasonsJson = "[\"Reason\"]",
        WarningsJson = "[]",
        Provider = "Test",
        Model = "Test",
        Status = ListingAiEnrichmentStatus.Succeeded,
    };
}
