namespace CasaSim.Api.Models;

public sealed class ListingAiDto
{
    public string? Summary { get; init; }
    public decimal? DealScore { get; init; }
    public string? DealLabel { get; init; }
    public List<string> DealReasons { get; init; } = [];
    public List<string> Warnings { get; init; } = [];
    public object? CorrectedFacts { get; init; }
}
