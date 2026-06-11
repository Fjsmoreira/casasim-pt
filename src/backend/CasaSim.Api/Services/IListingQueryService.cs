using CasaSim.Api.Models;

namespace CasaSim.Api.Services;

/// <summary>
/// Service for querying listing summaries with filtering, sorting, and pagination.
/// </summary>
public interface IListingQueryService
{
    /// <summary>
    /// Search listings with the given filters, returning a paged result.
    /// </summary>
    Task<PagedResult<ListingSummaryDto>> SearchAsync(ListingSearchRequest request, CancellationToken ct = default);
}
