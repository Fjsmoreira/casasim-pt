namespace CasaSim.Scraper.Configuration;

public sealed class AiOptions
{
    public bool Enabled { get; set; }
    public string Provider { get; set; } = "DeepSeek";
    public string Endpoint { get; set; } = "https://api.deepseek.com";
    public string Model { get; set; } = "deepseek-chat";
    public int BatchSize { get; set; } = 10;
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(5);

    public string? ResolveApiKey()
    {
        var generic = Environment.GetEnvironmentVariable("AI_API_KEY");
        if (!string.IsNullOrWhiteSpace(generic))
            return generic;

        return Provider.Equals("OpenAI", StringComparison.OrdinalIgnoreCase)
            ? Environment.GetEnvironmentVariable("OPENAI_API_KEY")
            : Environment.GetEnvironmentVariable("DEEPSEEK_API_KEY");
    }
}
