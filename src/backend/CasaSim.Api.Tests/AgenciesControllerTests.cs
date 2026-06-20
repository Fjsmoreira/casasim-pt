using CasaSim.Api;
using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Tests;

public class AgenciesControllerTests
{
    [Fact]
    public async Task GetActive_ReturnsOnlyActiveAgenciesAlphabetically()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AgencyTests_" + Guid.NewGuid())
            .Options;
        await using var db = new AppDbContext(options);
        db.Agencies.AddRange(
            new Agency { Name = "Zome", Slug = "zome", IsActive = true },
            new Agency { Name = "Argilipe", Slug = "argilipe", IsActive = true },
            new Agency { Name = "Inactive", Slug = "inactive", IsActive = false });
        await db.SaveChangesAsync();

        var result = await new AgenciesController(db).GetActive(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var agencies = Assert.IsAssignableFrom<IReadOnlyList<AgencyDto>>(ok.Value);
        Assert.Collection(agencies,
            agency => Assert.Equal("Argilipe", agency.Name),
            agency => Assert.Equal("Zome", agency.Name));
    }
}
