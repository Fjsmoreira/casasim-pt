using CasaSim.Core.Models;

namespace CasaSim.Core.Interfaces;

public interface IPropertyScraper
{
    string AgencyName { get; }
    Task<IReadOnlyList<Property>> ScrapeAsync(CancellationToken ct = default);
}
