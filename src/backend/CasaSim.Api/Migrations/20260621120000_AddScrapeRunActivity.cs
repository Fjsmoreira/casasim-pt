using System;
using CasaSim.Api;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace CasaSim.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260621120000_AddScrapeRunActivity")]
public partial class AddScrapeRunActivity : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "current_phase",
            table: "scrape_log",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "last_activity_at",
            table: "scrape_log",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "scrape_run_activity",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                scrape_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                level = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                phase = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                current_count = table.Column<int>(type: "integer", nullable: true),
                total_count = table.Column<int>(type: "integer", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scrape_run_activity", x => x.id);
                table.ForeignKey(
                    name: "fk_scrape_run_activity_scrape_log_scrape_log_id",
                    column: x => x.scrape_log_id,
                    principalTable: "scrape_log",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_scrape_run_activity_created_at",
            table: "scrape_run_activity",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_scrape_run_activity_scrape_log_id_created_at",
            table: "scrape_run_activity",
            columns: new[] { "scrape_log_id", "created_at" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "scrape_run_activity");
        migrationBuilder.DropColumn(name: "current_phase", table: "scrape_log");
        migrationBuilder.DropColumn(name: "last_activity_at", table: "scrape_log");
    }
}
