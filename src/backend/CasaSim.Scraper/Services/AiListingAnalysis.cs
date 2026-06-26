namespace CasaSim.Scraper.Services;

public sealed record AiListingAnalysis(
    string GeneratedDescription,
    string CorrectedFactsJson,
    string ExtractedFactsJson,
    string HighlightsJson,
    decimal DealScore,
    string DealLabel,
    string DealReasonsJson,
    string WarningsJson);

public interface IAiListingAnalyzer
{
    Task<AiListingAnalysis> AnalyzeAsync(ListingAiInput input, decimal deterministicDealScore, CancellationToken ct = default);
}

public sealed record ListingAiInput(
    Guid ListingId,
    string Title,
    string? Description,
    decimal? Price,
    string PriceType,
    string PropertyType,
    string? City,
    string? Parish,
    int? Bedrooms,
    int? Bathrooms,
    decimal? AreaM2,
    decimal? LandAreaM2,
    string? EnergyClass,
    string? AgencyName);
