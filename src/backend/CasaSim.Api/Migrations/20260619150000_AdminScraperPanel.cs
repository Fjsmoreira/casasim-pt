using System;
using CasaSim.Api;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable enable

namespace CasaSim.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260619150000_AdminScraperPanel")]
public partial class AdminScraperPanel : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "scraper_source",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                agency_id = table.Column<Guid>(type: "uuid", nullable: true),
                name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                scraper_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                agency_slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                target_description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                enabled = table.Column<bool>(type: "boolean", nullable: false),
                interval = table.Column<TimeSpan>(type: "interval", nullable: false),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scraper_source", x => x.id);
                table.ForeignKey(
                    name: "fk_scraper_source_agency_agency_id",
                    column: x => x.agency_id,
                    principalTable: "agency",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateTable(
            name: "scrape_listing_change",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                scrape_log_id = table.Column<Guid>(type: "uuid", nullable: false),
                listing_id = table.Column<Guid>(type: "uuid", nullable: true),
                action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                agency_slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                change_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("pk_scrape_listing_change", x => x.id);
                table.ForeignKey(
                    name: "fk_scrape_listing_change_listing_listing_id",
                    column: x => x.listing_id,
                    principalTable: "listing",
                    principalColumn: "id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "fk_scrape_listing_change_scrape_log_scrape_log_id",
                    column: x => x.scrape_log_id,
                    principalTable: "scrape_log",
                    principalColumn: "id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "ix_scraper_source_enabled",
            table: "scraper_source",
            column: "enabled");

        migrationBuilder.CreateIndex(
            name: "ix_scraper_source_scraper_key",
            table: "scraper_source",
            column: "scraper_key",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "ix_scraper_source_agency_id",
            table: "scraper_source",
            column: "agency_id");

        migrationBuilder.CreateIndex(
            name: "ix_scrape_listing_change_action",
            table: "scrape_listing_change",
            column: "action");

        migrationBuilder.CreateIndex(
            name: "ix_scrape_listing_change_created_at",
            table: "scrape_listing_change",
            column: "created_at");

        migrationBuilder.CreateIndex(
            name: "ix_scrape_listing_change_listing_id",
            table: "scrape_listing_change",
            column: "listing_id");

        migrationBuilder.CreateIndex(
            name: "ix_scrape_listing_change_scrape_log_id",
            table: "scrape_listing_change",
            column: "scrape_log_id");

        var now = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
        migrationBuilder.InsertData(
            table: "scraper_source",
            columns: new[] { "id", "agency_id", "name", "scraper_key", "agency_slug", "source_url", "target_description", "enabled", "interval", "created_at", "updated_at" },
            columnTypes: new[] { "uuid", "uuid", "character varying(255)", "character varying(100)", "character varying(255)", "character varying(2048)", "character varying(1000)", "boolean", "interval", "timestamp with time zone", "timestamp with time zone" },
            values: new object[,]
            {
                { Guid.Parse("b1000000-0000-0000-0000-000000000001"), Guid.Parse("a1000000-0000-0000-0000-000000000001"), "Remax Pombal", "Remax", "remax-pombal", "https://www.remax.pt", "Remax listings for Pombal", true, TimeSpan.FromMinutes(1), now, now },
                { Guid.Parse("b1000000-0000-0000-0000-000000000002"), Guid.Parse("a1000000-0000-0000-0000-000000000002"), "Century21 Pombal", "Century21", "century21-pombal", "https://www.century21.pt", "Century21 sale and rent listings for Pombal", true, TimeSpan.FromMinutes(1), now, now },
                { Guid.Parse("b1000000-0000-0000-0000-000000000003"), Guid.Parse("a1000000-0000-0000-0000-000000000003"), "ERA Pombal", "ERA", "era-pombal", "https://www.era.pt/imoveis/agencia/pombal", "ERA agency listings for Pombal", true, TimeSpan.FromMinutes(1), now, now }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "scrape_listing_change");
        migrationBuilder.DropTable(name: "scraper_source");
    }
}
