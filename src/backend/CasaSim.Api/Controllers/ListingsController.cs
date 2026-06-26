using CasaSim.Api.Models;
using CasaSim.Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/listings")]
public sealed class ListingsController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IListingQueryService _listingQuery;

    public ListingsController(AppDbContext db, IListingQueryService listingQuery)
    {
        _db = db;
        _listingQuery = listingQuery;
    }

    /// <summary>
    /// Search listings with filtering, sorting, and pagination.
    /// Returns a paged list of listing summaries.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<ListingSummaryDto>>> GetListings(
        [FromQuery] ListingSearchRequest request,
        CancellationToken ct)
    {
        var result = await _listingQuery.SearchAsync(request, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get a single listing by ID with full details (images, features, agency, location).
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ListingDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var listing = await _db.Listings
            .AsNoTracking()
            .Include(l => l.Agency)
            .Include(l => l.Location)
            .Include(l => l.Images.OrderBy(i => i.SortOrder))
            .Include(l => l.Features)
            .FirstOrDefaultAsync(l => l.Id == id, ct);

        if (listing is null)
            return NotFound(new { error = "Listing not found.", id });

        var enrichment = await _db.ListingAiEnrichments
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ListingId == id, ct);

        return Ok(new ListingDetailDto
        {
            Id = listing.Id,
            Title = listing.Title,
            Price = listing.Price,
            PriceType = listing.PriceType.ToString(),
            Currency = listing.Currency,
            PropertyType = listing.PropertyType.ToString(),
            Status = listing.Status.ToString(),
            City = listing.City,
            Parish = listing.Location?.Parish,
            District = listing.Location?.District,
            Description = listing.Description,
            Bedrooms = listing.Bedrooms,
            Bathrooms = listing.Bathrooms,
            AreaM2 = listing.AreaM2,
            LandAreaM2 = listing.LandAreaM2,
            Images = listing.Images
                .OrderBy(i => i.SortOrder)
                .Select(i => i.Url)
                .ToList(),
            PrimaryImage = listing.Images
                .Where(i => i.IsPrimary)
                .OrderBy(i => i.SortOrder)
                .Select(i => new ListingImageDto
                {
                    Id = i.Id,
                    Url = i.Url,
                    ThumbnailUrl = i.ThumbnailUrl,
                    AltText = i.AltText,
                    IsPrimary = i.IsPrimary,
                    SortOrder = i.SortOrder,
                })
                .FirstOrDefault(),
            Features = listing.Features
                .Select(f => f.Name)
                .ToList(),
            Ai = ListingAiDtoMapper.FromEnrichment(enrichment),
            SourceUrl = listing.SourceUrl,
            ExternalId = listing.ExternalId,
            AgencyName = listing.Agency?.Name,
            AgencyPhone = listing.Agency?.ContactPhone,
            AgencyEmail = listing.Agency?.ContactEmail,
            AgencyWebsiteUrl = listing.Agency?.WebsiteUrl,
            PublishedAt = listing.PublishedAt,
            FirstSeenAt = listing.FirstSeenAt,
            LastSeenAt = listing.LastSeenAt,
            CreatedAt = listing.CreatedAt,
            UpdatedAt = listing.UpdatedAt,
        });
    }
}
