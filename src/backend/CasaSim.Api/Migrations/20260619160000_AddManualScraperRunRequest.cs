using System;
using CasaSim.Api;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace CasaSim.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260619160000_AddManualScraperRunRequest")]
public partial class AddManualScraperRunRequest : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "manual_run_requested_at",
            table: "scraper_source",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "manual_run_requested_at",
            table: "scraper_source");
    }
}
