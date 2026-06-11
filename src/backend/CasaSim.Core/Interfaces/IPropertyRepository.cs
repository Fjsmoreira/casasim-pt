using CasaSim.Core.Models;

namespace CasaSim.Core.Interfaces;

public interface IPropertyRepository
{
    Task<Property?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Property?> GetByExternalIdAsync(string externalId, string sourceAgency, CancellationToken ct = default);
    Task<IReadOnlyList<Property>> SearchAsync(PropertySearchCriteria criteria, CancellationToken ct = default);
    Task AddAsync(Property property, CancellationToken ct = default);
    Task UpdateAsync(Property property, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}

public sealed record PropertySearchCriteria
{
    public string? City { get; init; }
    public PropertyType? Type { get; init; }
    public TransactionType? Transaction { get; init; }
    public decimal? MinPrice { get; init; }
    public decimal? MaxPrice { get; init; }
    public int? MinBedrooms { get; init; }
    public int? Page { get; init; } = 1;
    public int? PageSize { get; init; } = 20;
    public string? SortBy { get; init; } = "updatedAt";
    public bool SortDescending { get; init; } = true;
}
