namespace CasaSim.Core.Data.Entities;

public sealed class ScraperSource
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public string Name { get; set; } = string.Empty;
    public string ScraperKey { get; set; } = string.Empty;
    public string AgencySlug { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public string? TargetDescription { get; set; }
    public bool Enabled { get; set; } = true;
    public TimeSpan Interval { get; set; } = TimeSpan.FromHours(6);
    public DateTimeOffset? ManualRunRequestedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
