namespace CasaSim.Api.Models;

public sealed class AgencyDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? WebsiteUrl { get; init; }
    public string? ContactEmail { get; init; }
    public string? ContactPhone { get; init; }
}
