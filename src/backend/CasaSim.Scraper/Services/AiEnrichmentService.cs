using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CasaSim.Api;
using CasaSim.Core.Data.Entities;
using CasaSim.Scraper.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CasaSim.Scraper.Services;

public sealed class AiEnrichmentService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptions<AiOptions> _options;
    private readonly ILogger<AiEnrichmentService> _logger;

    public AiEnrichmentService(
        IServiceScopeFactory scopeFactory,
        IOptions<AiOptions> options,
        ILogger<AiEnrichmentService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var options = _options.Value;
        if (!options.Enabled)
        {
            _logger.LogInformation("AI listing enrichment is disabled");
            return;
        }

        if (string.IsNullOrWhiteSpace(options.ResolveApiKey()))
        {
            _logger.LogError("AI listing enrichment is enabled but no API key is configured for provider {Provider}", options.Provider);
            return;
        }

        _logger.LogInformation("AI listing enrichment enabled with provider {Provider} and model {Model}", options.Provider, options.Model);

        using var timer = new PeriodicTimer(options.Interval > TimeSpan.Zero ? options.Interval : TimeSpan.FromMinutes(5));
        while (!stoppingToken.IsCancellationRequested)
        {
            await ProcessBatchAsync(stoppingToken);
            await timer.WaitForNextTickAsync(stoppingToken);
        }
    }

    internal async Task ProcessBatchAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var analyzer = scope.ServiceProvider.GetRequiredService<IAiListingAnalyzer>();
        var options = _options.Value;
        var now = DateTimeOffset.UtcNow;

        var candidates = await db.Listings
            .AsTracking()
            .Include(l => l.Agency)
            .Include(l => l.Location)
            .Where(l => l.Status == ListingStatus.Active)
            .OrderByDescending(l => l.UpdatedAt)
            .Take(Math.Max(options.BatchSize * 5, 50))
            .ToListAsync(ct);

        var candidateIds = candidates.Select(l => l.Id).ToArray();
        var enrichments = await db.ListingAiEnrichments
            .Where(e => candidateIds.Contains(e.ListingId))
            .ToDictionaryAsync(e => e.ListingId, ct);

        var processed = 0;
        foreach (var listing in candidates)
        {
            if (processed >= Math.Max(options.BatchSize, 1))
                break;

            var input = ToAiInput(listing);
            var sourceHash = ComputeSourceHash(input);

            enrichments.TryGetValue(listing.Id, out var enrichment);
            if (enrichment is not null &&
                enrichment.SourceHash == sourceHash &&
                enrichment.Status == ListingAiEnrichmentStatus.Succeeded)
            {
                continue;
            }

            if (enrichment?.NextRetryAt is not null && enrichment.NextRetryAt > now)
                continue;

            enrichment ??= new ListingAiEnrichment
            {
                Id = Guid.NewGuid(),
                ListingId = listing.Id,
                CreatedAt = now,
            };

            try
            {
                var score = CalculateDealScore(input);
                var analysis = await analyzer.AnalyzeAsync(input, score, ct);

                enrichment.SourceHash = sourceHash;
                enrichment.GeneratedDescription = analysis.GeneratedDescription;
                enrichment.CorrectedFactsJson = analysis.CorrectedFactsJson;
                enrichment.CorrectionAuditJson = ApplyCorrections(listing, analysis.CorrectedFactsJson);
                enrichment.ExtractedFactsJson = analysis.ExtractedFactsJson;
                enrichment.HighlightsJson = analysis.HighlightsJson;
                enrichment.DealScore = analysis.DealScore;
                enrichment.DealLabel = analysis.DealLabel;
                enrichment.DealReasonsJson = analysis.DealReasonsJson;
                enrichment.WarningsJson = analysis.WarningsJson;
                enrichment.Provider = options.Provider;
                enrichment.Model = options.Model;
                enrichment.Status = ListingAiEnrichmentStatus.Succeeded;
                enrichment.LastError = null;
                enrichment.LastAnalyzedAt = now;
                enrichment.NextRetryAt = null;
                enrichment.AttemptCount++;
                enrichment.UpdatedAt = now;

                if (db.Entry(enrichment).State == EntityState.Detached)
                    db.ListingAiEnrichments.Add(enrichment);

                processed++;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "AI enrichment failed for listing {ListingId}", listing.Id);

                enrichment.SourceHash = sourceHash;
                enrichment.Provider = options.Provider;
                enrichment.Model = options.Model;
                enrichment.Status = ListingAiEnrichmentStatus.Failed;
                enrichment.LastError = ex.Message;
                enrichment.AttemptCount++;
                enrichment.NextRetryAt = now.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(enrichment.AttemptCount, 5))));
                enrichment.UpdatedAt = now;

                if (db.Entry(enrichment).State == EntityState.Detached)
                    db.ListingAiEnrichments.Add(enrichment);
            }
        }

        if (db.ChangeTracker.HasChanges())
            await db.SaveChangesAsync(ct);
    }

    internal static string ComputeSourceHash(ListingAiInput input)
    {
        var json = JsonSerializer.Serialize(input);
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    internal static decimal CalculateDealScore(ListingAiInput input)
    {
        if (input.Price is null or <= 0)
            return 0m;

        var score = 50m;
        if (input.AreaM2 is > 0)
        {
            var pricePerM2 = input.Price.Value / input.AreaM2.Value;
            score += pricePerM2 switch
            {
                <= 900m => 30m,
                <= 1200m => 20m,
                <= 1600m => 10m,
                <= 2200m => 0m,
                _ => -15m,
            };
        }
        else
        {
            score -= 20m;
        }

        if (input.Bedrooms is >= 3 && input.PropertyType.Equals("House", StringComparison.OrdinalIgnoreCase))
            score += 5m;
        if (string.IsNullOrWhiteSpace(input.Description))
            score -= 5m;
        if (string.IsNullOrWhiteSpace(input.City))
            score -= 5m;

        return Math.Clamp(score, 0m, 100m);
    }

    internal static string GetDealLabel(decimal score) => score switch
    {
        >= 70m => "GoodDeal",
        <= 40m => "BadDeal",
        _ => "Neutral",
    };

    private static string? ApplyCorrections(Listing listing, string correctedFactsJson)
    {
        using var doc = JsonDocument.Parse(correctedFactsJson);
        var root = doc.RootElement;
        var facts = root.TryGetProperty("correctedFacts", out var nestedFacts) ? nestedFacts : root;
        var confidence = root.TryGetProperty("fieldConfidence", out var fieldConfidence) ? fieldConfidence : default;
        var changes = new Dictionary<string, object?>();

        if (TryGetString(facts, "propertyType", out var propertyType) &&
            TryGetConfidence(confidence, "propertyType") >= 0.75m &&
            Enum.TryParse<ListingPropertyType>(propertyType, ignoreCase: true, out var parsedType) &&
            listing.PropertyType is ListingPropertyType.Unknown or ListingPropertyType.Other)
        {
            changes["propertyType"] = new { before = listing.PropertyType.ToString(), after = parsedType.ToString(), confidence = TryGetConfidence(confidence, "propertyType") };
            listing.PropertyType = parsedType;
        }

        if (TryGetString(facts, "transaction", out var transaction) &&
            TryGetConfidence(confidence, "transaction") >= 0.75m &&
            Enum.TryParse<ListingPriceType>(transaction, ignoreCase: true, out var parsedPriceType) &&
            listing.PriceType == ListingPriceType.Unknown)
        {
            changes["priceType"] = new { before = listing.PriceType.ToString(), after = parsedPriceType.ToString(), confidence = TryGetConfidence(confidence, "transaction") };
            listing.PriceType = parsedPriceType;
        }

        ApplyIntCorrection(changes, facts, confidence, "bedrooms", listing.Bedrooms, value => listing.Bedrooms = value);
        ApplyIntCorrection(changes, facts, confidence, "bathrooms", listing.Bathrooms, value => listing.Bathrooms = value);
        ApplyDecimalCorrection(changes, facts, confidence, "areaM2", listing.AreaM2, value => listing.AreaM2 = value);
        ApplyDecimalCorrection(changes, facts, confidence, "landAreaM2", listing.LandAreaM2, value => listing.LandAreaM2 = value);

        if (TryGetString(facts, "locality", out var locality) &&
            TryGetConfidence(confidence, "locality") >= 0.75m &&
            !string.IsNullOrWhiteSpace(locality) &&
            string.IsNullOrWhiteSpace(listing.Location?.Parish))
        {
            listing.Location ??= new Location
            {
                Municipality = listing.City ?? "Pombal",
                District = "Leiria",
                CountryCode = "PT",
                CreatedAt = DateTimeOffset.UtcNow,
                FirstSeenAt = DateTimeOffset.UtcNow,
                LastSeenAt = DateTimeOffset.UtcNow,
            };
            changes["locality"] = new { before = listing.Location.Parish, after = locality, confidence = TryGetConfidence(confidence, "locality") };
            listing.Location.Parish = locality;
            listing.Location.UpdatedAt = DateTimeOffset.UtcNow;
        }

        if (changes.Count == 0)
            return null;

        listing.UpdatedAt = DateTimeOffset.UtcNow;
        return JsonSerializer.Serialize(changes);
    }

    private static void ApplyIntCorrection(
        IDictionary<string, object?> changes,
        JsonElement facts,
        JsonElement confidence,
        string field,
        int? current,
        Action<int> apply)
    {
        if (current is > 0 || TryGetConfidence(confidence, field) < 0.75m || !TryGetInt(facts, field, out var value) || value <= 0)
            return;

        changes[field] = new { before = current, after = value, confidence = TryGetConfidence(confidence, field) };
        apply(value);
    }

    private static void ApplyDecimalCorrection(
        IDictionary<string, object?> changes,
        JsonElement facts,
        JsonElement confidence,
        string field,
        decimal? current,
        Action<decimal> apply)
    {
        if (current is > 0 || TryGetConfidence(confidence, field) < 0.75m || !TryGetDecimal(facts, field, out var value) || value <= 0)
            return;

        changes[field] = new { before = current, after = value, confidence = TryGetConfidence(confidence, field) };
        apply(value);
    }

    private static bool TryGetString(JsonElement element, string property, out string value)
    {
        value = string.Empty;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var propertyElement) || propertyElement.ValueKind != JsonValueKind.String)
            return false;

        value = propertyElement.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetInt(JsonElement element, string property, out int value)
    {
        value = 0;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var propertyElement))
            return false;

        return propertyElement.ValueKind switch
        {
            JsonValueKind.Number => propertyElement.TryGetInt32(out value),
            JsonValueKind.String => int.TryParse(propertyElement.GetString(), out value),
            _ => false,
        };
    }

    private static bool TryGetDecimal(JsonElement element, string property, out decimal value)
    {
        value = 0m;
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(property, out var propertyElement))
            return false;

        return propertyElement.ValueKind switch
        {
            JsonValueKind.Number => propertyElement.TryGetDecimal(out value),
            JsonValueKind.String => decimal.TryParse(propertyElement.GetString(), out value),
            _ => false,
        };
    }

    private static decimal TryGetConfidence(JsonElement confidence, string property)
    {
        if (confidence.ValueKind != JsonValueKind.Object || !confidence.TryGetProperty(property, out var value))
            return 0m;

        return value.ValueKind switch
        {
            JsonValueKind.Number when value.TryGetDecimal(out var number) => Math.Clamp(number, 0m, 1m),
            JsonValueKind.String when decimal.TryParse(value.GetString(), out var number) => Math.Clamp(number, 0m, 1m),
            _ => 0m,
        };
    }

    private static ListingAiInput ToAiInput(Listing listing) => new(
        listing.Id,
        listing.Title,
        listing.Description,
        listing.Price,
        listing.PriceType.ToString(),
        listing.PropertyType.ToString(),
        listing.City,
        listing.Location?.Parish,
        listing.Bedrooms,
        listing.Bathrooms,
        listing.AreaM2,
        listing.LandAreaM2,
        listing.EnergyClass,
        listing.Agency?.Name);
}
