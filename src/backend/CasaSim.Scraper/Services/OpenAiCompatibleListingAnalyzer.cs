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
        "extractedFacts": {
          "type": "object",
          "additionalProperties": false,
          "properties": {
            "condition": { "type": "string" },
            "locationQuality": { "type": "string" },
            "idealBuyer": { "type": "string" },
            "renovationHint": { "type": "string" }
          },
          "required": ["condition", "locationQuality", "idealBuyer", "renovationHint"]
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
      "required": ["generatedDescription", "extractedFacts", "highlights", "dealReasons", "warnings"],
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
            instructions = "Analyze this Portuguese real estate listing. Return only valid JSON matching this shape: { generatedDescription: string, extractedFacts: { condition: string, locationQuality: string, idealBuyer: string, renovationHint: string }, highlights: string[], dealReasons: string[], warnings: string[] }. Do not invent missing facts. Use warnings for uncertainty.",
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
            root.GetProperty("extractedFacts").GetRawText(),
            root.GetProperty("highlights").GetRawText(),
            deterministicDealScore,
            root.GetProperty("dealReasons").GetRawText(),
            root.GetProperty("warnings").GetRawText());
    }
}
