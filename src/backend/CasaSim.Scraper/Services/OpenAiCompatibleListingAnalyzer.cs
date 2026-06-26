using System.ClientModel;
using System.Text.Json;
using CasaSim.Scraper.Configuration;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace CasaSim.Scraper.Services;

public sealed class OpenAiCompatibleListingAnalyzer : IAiListingAnalyzer
{
    private static readonly BinaryData ResponseSchema = BinaryData.FromString("""
    {
      "type": "object",
      "properties": {
        "generatedDescription": { "type": "string" },
        "correctedFacts": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "propertyType": { "type": ["string", "null"] },
            "transaction": { "type": ["string", "null"] },
            "locality": { "type": ["string", "null"] },
            "bedrooms": { "type": ["integer", "null"] },
            "bathrooms": { "type": ["integer", "null"] },
            "areaM2": { "type": ["number", "null"] },
            "landAreaM2": { "type": ["number", "null"] },
            "condition": { "type": "string" },
            "renovationNeed": { "type": "string" }
          },
          "required": ["propertyType", "transaction", "locality", "bedrooms", "bathrooms", "areaM2", "landAreaM2", "condition", "renovationNeed"]
        },
        "fieldConfidence": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "propertyType": { "type": "number" },
            "transaction": { "type": "number" },
            "locality": { "type": "number" },
            "bedrooms": { "type": "number" },
            "bathrooms": { "type": "number" },
            "areaM2": { "type": "number" },
            "landAreaM2": { "type": "number" },
            "condition": { "type": "number" },
            "renovationNeed": { "type": "number" }
          },
          "required": ["propertyType", "transaction", "locality", "bedrooms", "bathrooms", "areaM2", "landAreaM2", "condition", "renovationNeed"]
        },
        "highlights": {
          "type": "array",
          "items": { "type": "string" }
        },
        "dealReasons": {
          "type": "array",
          "items": { "type": "string" }
        },
        "warnings": {
          "type": "array",
          "items": { "type": "string" }
        }
      },
      "required": ["generatedDescription", "correctedFacts", "fieldConfidence", "highlights", "dealReasons", "warnings"],
      "additionalProperties": false
    }
    """);

    private readonly ChatClient _client;
    private readonly bool _useStrictJsonSchema;

    public OpenAiCompatibleListingAnalyzer(IOptions<AiOptions> options)
    {
        var config = options.Value;
        var apiKey = config.ResolveApiKey();
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new InvalidOperationException("AI is enabled but no API key was configured.");
        if (string.IsNullOrWhiteSpace(config.Model))
            throw new InvalidOperationException("AI is enabled but no model was configured.");
        if (!Uri.TryCreate(config.Endpoint, UriKind.Absolute, out var endpoint))
            throw new InvalidOperationException("AI is enabled but the endpoint is invalid.");

        _client = new ChatClient(
            model: config.Model,
            credential: new ApiKeyCredential(apiKey),
            options: new OpenAIClientOptions
            {
                Endpoint = endpoint,
                NetworkTimeout = TimeSpan.FromMinutes(2),
            });
        _useStrictJsonSchema = config.Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase);
    }

    public async Task<AiListingAnalysis> AnalyzeAsync(
        ListingAiInput input,
        decimal deterministicDealScore,
        CancellationToken ct = default)
    {
        var prompt = JsonSerializer.Serialize(new
        {
            instructions = "Analyze this Portuguese real estate listing in Portuguese. Return only valid JSON matching this shape: { generatedDescription: string, correctedFacts: { propertyType, transaction, locality, bedrooms, bathrooms, areaM2, landAreaM2, condition, renovationNeed }, fieldConfidence: { propertyType, transaction, locality, bedrooms, bathrooms, areaM2, landAreaM2, condition, renovationNeed }, highlights: string[], dealReasons: string[], warnings: string[] }. Correct facts only when directly supported by the listing text. Do not invent missing facts. Use null and low confidence for uncertainty. Use warnings for caveats, not factual claims.",
            deterministicDealScore,
            listing = input,
        });

        ChatCompletionOptions options = new();
        if (_useStrictJsonSchema)
        {
            options.ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "casasim_listing_analysis",
                jsonSchema: ResponseSchema,
                jsonSchemaIsStrict: true);
        }

        ClientResult<ChatCompletion> result = await _client.CompleteChatAsync(
            [new UserChatMessage(prompt)],
            options,
            ct);

        var content = result.Value.Content.FirstOrDefault()?.Text;
        if (string.IsNullOrWhiteSpace(content))
            throw new InvalidOperationException("AI response did not include text content.");

        using var doc = JsonDocument.Parse(content);
        var root = doc.RootElement;
        var description = root.GetProperty("generatedDescription").GetString();
        if (string.IsNullOrWhiteSpace(description))
            throw new InvalidOperationException("AI response did not include generatedDescription.");

        return new AiListingAnalysis(
            description,
            JsonSerializer.Serialize(new
            {
                correctedFacts = JsonSerializer.Deserialize<object>(root.GetProperty("correctedFacts").GetRawText()),
                fieldConfidence = JsonSerializer.Deserialize<object>(root.GetProperty("fieldConfidence").GetRawText()),
            }),
            root.GetProperty("correctedFacts").GetRawText(),
            root.GetProperty("highlights").GetRawText(),
            deterministicDealScore,
            AiEnrichmentService.GetDealLabel(deterministicDealScore),
            root.GetProperty("dealReasons").GetRawText(),
            root.GetProperty("warnings").GetRawText());
    }
}
