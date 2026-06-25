using CasaSim.Core.Models;

namespace CasaSim.Core.Interfaces;

public interface IIncrementalPropertyScraper
{
    Task<IReadOnlyList<Property>> ScrapeAsync(ScrapeRequest request, CancellationToken ct = default);
}

public sealed record ScrapeRequest(
    ScrapeMode Mode,
    IReadOnlySet<string> KnownExternalIds,
    int KnownListingStopThreshold);

public enum ScrapeMode
{
    Incremental = 0,
    Full,
}
