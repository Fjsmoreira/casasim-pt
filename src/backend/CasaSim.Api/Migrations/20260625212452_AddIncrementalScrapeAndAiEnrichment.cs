using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaSim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddIncrementalScrapeAndAiEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "force_full_scrape",
                table: "scraper_source",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "incremental_known_listing_threshold",
                table: "scraper_source",
                type: "integer",
                nullable: false,
                defaultValue: 10);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "last_full_scrape_at",
                table: "scraper_source",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "latest_known_external_ids_json",
                table: "scraper_source",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "listing_ai_enrichment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_hash = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    generated_description = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    extracted_facts_json = table.Column<string>(type: "jsonb", nullable: true),
                    highlights_json = table.Column<string>(type: "jsonb", nullable: true),
                    deal_score = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    deal_reasons_json = table.Column<string>(type: "jsonb", nullable: true),
                    warnings_json = table.Column<string>(type: "jsonb", nullable: true),
                    provider = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attempt_count = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    last_analyzed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_retry_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_ai_enrichment", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_ai_enrichment_listing_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listing",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "scraper_source",
                keyColumn: "id",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000001"),
                columns: new[] { "force_full_scrape", "incremental_known_listing_threshold", "last_full_scrape_at", "latest_known_external_ids_json" },
                values: new object[] { false, 10, null, null });

            migrationBuilder.UpdateData(
                table: "scraper_source",
                keyColumn: "id",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000002"),
                columns: new[] { "force_full_scrape", "incremental_known_listing_threshold", "last_full_scrape_at", "latest_known_external_ids_json" },
                values: new object[] { false, 10, null, null });

            migrationBuilder.UpdateData(
                table: "scraper_source",
                keyColumn: "id",
                keyValue: new Guid("b1000000-0000-0000-0000-000000000003"),
                columns: new[] { "force_full_scrape", "incremental_known_listing_threshold", "last_full_scrape_at", "latest_known_external_ids_json" },
                values: new object[] { false, 10, null, null });

            migrationBuilder.CreateIndex(
                name: "ix_listing_ai_enrichment_listing_id",
                table: "listing_ai_enrichment",
                column: "listing_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_ai_enrichment_next_retry_at",
                table: "listing_ai_enrichment",
                column: "next_retry_at");

            migrationBuilder.CreateIndex(
                name: "ix_listing_ai_enrichment_status",
                table: "listing_ai_enrichment",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "listing_ai_enrichment");

            migrationBuilder.DropColumn(
                name: "force_full_scrape",
                table: "scraper_source");

            migrationBuilder.DropColumn(
                name: "incremental_known_listing_threshold",
                table: "scraper_source");

            migrationBuilder.DropColumn(
                name: "last_full_scrape_at",
                table: "scraper_source");

            migrationBuilder.DropColumn(
                name: "latest_known_external_ids_json",
                table: "scraper_source");
        }
    }
}
