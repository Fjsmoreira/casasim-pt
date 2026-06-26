using CasaSim.Scraper.Services;

namespace CasaSim.Scraper.Tests;

public sealed class AiEnrichmentServiceTests
{
    [Fact]
    public void ComputeSourceHash_Changes_When_Input_Changes()
    {
        var original = MakeInput(price: 185000m);
        var changed = MakeInput(price: 175000m);

        Assert.NotEqual(
            AiEnrichmentService.ComputeSourceHash(original),
            AiEnrichmentService.ComputeSourceHash(changed));
    }

    [Fact]
    public void CalculateDealScore_ReturnsZero_When_Price_Missing()
    {
        var input = MakeInput(price: null);

        Assert.Equal(0m, AiEnrichmentService.CalculateDealScore(input));
    }

    [Fact]
    public void CalculateDealScore_Rewards_Low_Price_Per_M2()
    {
        var goodDeal = MakeInput(price: 90000m, areaM2: 120m);
        var expensive = MakeInput(price: 300000m, areaM2: 120m);

        Assert.True(
            AiEnrichmentService.CalculateDealScore(goodDeal) >
            AiEnrichmentService.CalculateDealScore(expensive));
    }

    [Theory]
    [InlineData(70, "GoodDeal")]
    [InlineData(69.99, "Neutral")]
    [InlineData(40.01, "Neutral")]
    [InlineData(40, "BadDeal")]
    public void GetDealLabel_Uses_Configured_Thresholds(decimal score, string expected)
    {
        Assert.Equal(expected, AiEnrichmentService.GetDealLabel(score));
    }

    private static ListingAiInput MakeInput(decimal? price, decimal? areaM2 = 150m) => new(
        Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
        "Moradia T3 em Pombal",
        "Moradia com garagem e jardim.",
        price,
        "Sale",
        "House",
        "Pombal",
        "Abiul",
        3,
        2,
        areaM2,
        500m,
        "C",
        "Remax Pombal");
}
