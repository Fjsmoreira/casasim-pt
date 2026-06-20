using CasaSim.Api.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/agencies")]
public sealed class AgenciesController : ControllerBase
{
    private readonly AppDbContext _db;

    public AgenciesController(AppDbContext db) => _db = db;

    /// <summary>Returns agencies available for public listing search.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgencyDto>>> GetActive(CancellationToken ct)
    {
        var agencies = await _db.Agencies
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Name)
            .Select(a => new AgencyDto
            {
                Id = a.Id,
                Name = a.Name,
                Slug = a.Slug,
                WebsiteUrl = a.WebsiteUrl,
                ContactEmail = a.ContactEmail,
                ContactPhone = a.ContactPhone,
            })
            .ToListAsync(ct);

        return Ok(agencies);
    }
}
