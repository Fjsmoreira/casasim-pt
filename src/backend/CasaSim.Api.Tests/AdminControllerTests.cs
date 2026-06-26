using CasaSim.Api;
using CasaSim.Api.Controllers;
using CasaSim.Api.Models;
using CasaSim.Core.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CasaSim.Api.Tests;

public sealed class AdminControllerTests
{
    [Fact]
    public async Task GetListings_Clamps_Invalid_Pagination()
    {
        await using var db = CreateDb();
        var result = await new AdminController(db).GetListings(page: -2, pageSize: 500, ct: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var page = Assert.IsAssignableFrom<PagedResult<AdminListingDto>>(ok.Value);
        Assert.Equal(1, page.Page);
        Assert.Equal(100, page.PageSize);
    }

    private static AppDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase("AdminControllerTests_" + Guid.NewGuid())
            .Options;
        return new AppDbContext(options);
    }
}
