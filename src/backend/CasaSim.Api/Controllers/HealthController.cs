using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Liveness / readiness probe for Docker healthcheck and orchestration.
    /// Returns 200 with database connectivity status.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var dbOk = false;
        try
        {
            dbOk = await _db.Database.CanConnectAsync();
        }
        catch
        {
            // dbOk stays false
        }

        if (!dbOk)
            return StatusCode(503, new { status = "unhealthy", database = "unreachable" });

        return Ok(new { status = "healthy", database = "connected" });
    }
}
