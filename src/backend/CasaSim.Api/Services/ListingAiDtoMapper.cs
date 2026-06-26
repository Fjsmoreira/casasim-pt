using System.Text.Json;
using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;

namespace CasaSim.Api.Services;

internal static class ListingAiDtoMapper
{
    public static ListingAiDto? FromEnrichment(ListingAiEnrichment? enrichment)
    {
        if (enrichment is null || enrichment.Status != ListingAiEnrichmentStatus.Succeeded)
            return null;

        return new ListingAiDto
        {
            Summary = enrichment.GeneratedDescription,
            DealScore = enrichment.DealScore,
            DealLabel = enrichment.DealLabel,
            DealReasons = ReadStringArray(enrichment.DealReasonsJson),
            Warnings = ReadStringArray(enrichment.WarningsJson),
            CorrectedFacts = ReadJsonObject(enrichment.CorrectedFactsJson ?? enrichment.ExtractedFactsJson),
        };
    }

    private static List<string> ReadStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array)
                return [];

            return doc.RootElement
                .EnumerateArray()
                .Select(item => item.ValueKind == JsonValueKind.String ? item.GetString() : item.GetRawText())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static object? ReadJsonObject(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<object>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
