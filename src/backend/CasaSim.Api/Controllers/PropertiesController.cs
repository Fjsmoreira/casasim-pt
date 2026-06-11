using CasaSim.Api.Models;
using CasaSim.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace CasaSim.Api.Controllers;

/// <summary>
/// Backwards-compatible alias for older frontend builds that still call /api/properties.
/// The canonical public endpoint is /api/listings.
/// </summary>
[ApiController]
[Route("api/properties")]
public sealed class PropertiesController : ControllerBase
{
    private readonly IListingQueryService _listingQuery;

    public PropertiesController(IListingQueryService listingQuery) => _listingQuery = listingQuery;

    [HttpGet]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> Search(
        [FromQuery] ListingSearchRequest request,
        CancellationToken ct)
    {
        var result = await _listingQuery.SearchAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        return RedirectPermanent($"/api/listings/{id}");
    }
}
