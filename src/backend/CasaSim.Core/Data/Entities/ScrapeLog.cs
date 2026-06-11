namespace CasaSim.Core.Data.Entities;

public sealed class ScrapeLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? AgencyId { get; set; }
    public Agency? Agency { get; set; }

    public string SourceName { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
    public ScrapeStatus Status { get; set; } = ScrapeStatus.Started;
    public DateTimeOffset StartedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAt { get; set; }
    public int ListingsFound { get; set; }
    public int ListingsCreated { get; set; }
    public int ListingsUpdated { get; set; }
    public int ListingsRemoved { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorDetails { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset FirstSeenAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset LastSeenAt { get; set; } = DateTimeOffset.UtcNow;
}

public enum ScrapeStatus
{
    Started = 0,
    Succeeded,
    PartiallySucceeded,
    Failed,
    Cancelled
}
