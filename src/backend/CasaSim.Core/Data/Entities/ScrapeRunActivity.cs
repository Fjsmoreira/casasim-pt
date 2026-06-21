namespace CasaSim.Core.Data.Entities;

/// <summary>
/// An operator-facing milestone emitted during a scraper run. These records are
/// intentionally compact; detailed application logs remain in the deployment log sink.
/// </summary>
public sealed class ScrapeRunActivity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ScrapeLogId { get; set; }
    public ScrapeLog? ScrapeLog { get; set; }
    public ScrapeActivityLevel Level { get; set; } = ScrapeActivityLevel.Information;
    public string Phase { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? CurrentCount { get; set; }
    public int? TotalCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ScrapeActivityLevel
{
    Information = 0,
    Warning,
    Error,
}
