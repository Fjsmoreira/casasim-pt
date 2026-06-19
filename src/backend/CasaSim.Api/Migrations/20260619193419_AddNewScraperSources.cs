using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace CasaSim.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddNewScraperSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listing_features_listings_listing_id",
                table: "listing_features");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_images_listings_listing_id",
                table: "listing_images");

            migrationBuilder.DropForeignKey(
                name: "fk_listings_agencies_agency_id",
                table: "listings");

            migrationBuilder.DropForeignKey(
                name: "fk_listings_locations_location_id",
                table: "listings");

            migrationBuilder.DropForeignKey(
                name: "fk_scrape_logs_agencies_agency_id",
                table: "scrape_logs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_scrape_logs",
                table: "scrape_logs");

            migrationBuilder.DropPrimaryKey(
                name: "pk_locations",
                table: "locations");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listings",
                table: "listings");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing_images",
                table: "listing_images");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing_features",
                table: "listing_features");

            migrationBuilder.DropPrimaryKey(
                name: "pk_agencies",
                table: "agencies");

            migrationBuilder.RenameTable(
                name: "scrape_logs",
                newName: "scrape_log");

            migrationBuilder.RenameTable(
                name: "locations",
                newName: "location");

            migrationBuilder.RenameTable(
                name: "listings",
                newName: "listing");

            migrationBuilder.RenameTable(
                name: "listing_images",
                newName: "listing_image");

            migrationBuilder.RenameTable(
                name: "listing_features",
                newName: "listing_feature");

            migrationBuilder.RenameTable(
                name: "agencies",
                newName: "agency");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_logs_status",
                table: "scrape_log",
                newName: "ix_scrape_log_status");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_logs_started_at",
                table: "scrape_log",
                newName: "ix_scrape_log_started_at");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_logs_agency_id",
                table: "scrape_log",
                newName: "ix_scrape_log_agency_id");

            migrationBuilder.RenameIndex(
                name: "ix_locations_municipality",
                table: "location",
                newName: "ix_location_municipality");

            migrationBuilder.RenameIndex(
                name: "ix_locations_coordinate",
                table: "location",
                newName: "ix_location_coordinate");

            migrationBuilder.RenameIndex(
                name: "ix_listings_status",
                table: "listing",
                newName: "ix_listing_status");

            migrationBuilder.RenameIndex(
                name: "ix_listings_property_type",
                table: "listing",
                newName: "ix_listing_property_type");

            migrationBuilder.RenameIndex(
                name: "ix_listings_price",
                table: "listing",
                newName: "ix_listing_price");

            migrationBuilder.RenameIndex(
                name: "ix_listings_location_id",
                table: "listing",
                newName: "ix_listing_location_id");

            migrationBuilder.RenameIndex(
                name: "ix_listings_city",
                table: "listing",
                newName: "ix_listing_city");

            migrationBuilder.RenameIndex(
                name: "ix_listings_agency_id_external_id",
                table: "listing",
                newName: "ix_listing_agency_id_external_id");

            migrationBuilder.RenameIndex(
                name: "ix_listing_images_listing_id",
                table: "listing_image",
                newName: "ix_listing_image_listing_id");

            migrationBuilder.RenameIndex(
                name: "ix_listing_features_listing_id",
                table: "listing_feature",
                newName: "ix_listing_feature_listing_id");

            migrationBuilder.RenameIndex(
                name: "ix_agencies_slug",
                table: "agency",
                newName: "ix_agency_slug");

            migrationBuilder.AddPrimaryKey(
                name: "pk_scrape_log",
                table: "scrape_log",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_location",
                table: "location",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing",
                table: "listing",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing_image",
                table: "listing_image",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing_feature",
                table: "listing_feature",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_agency",
                table: "agency",
                column: "id");

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

            migrationBuilder.InsertData(
                table: "scraper_source",
                columns: new[] { "id", "agency_id", "agency_slug", "created_at", "enabled", "interval", "name", "scraper_key", "source_url", "target_description", "updated_at" },
                values: new object[,]
                {
                    { new Guid("b1000000-0000-0000-0000-000000000001"), new Guid("a1000000-0000-0000-0000-000000000001"), "remax-pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 0, 1, 0, 0), "Remax Pombal", "Remax", "https://www.remax.pt", "Remax listings for Pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000002"), new Guid("a1000000-0000-0000-0000-000000000002"), "century21-pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 0, 1, 0, 0), "Century21 Pombal", "Century21", "https://www.century21.pt", "Century21 sale and rent listings for Pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000003"), new Guid("a1000000-0000-0000-0000-000000000003"), "era-pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 0, 1, 0, 0), "ERA Pombal", "ERA", "https://www.era.pt/imoveis/agencia/pombal", "ERA agency listings for Pombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000004"), null, "valorfin-imoveis", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Valorfin Imóveis", "Valorfin Imóveis", "https://valorfinimoveis.pt", "CRM360 platform — Valorfin Imóveis", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000005"), null, "argilipe", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Argilipe", "Argilipe", "https://www.argilipe.pt", "CRM360 platform — Argilipe Imobiliária", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000006"), null, "imopombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "ImoPombal", "ImoPombal", "https://www.imopombal.pt", "eGO platform — ImoPombal", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000007"), null, "lionscastles", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "LionsCastles", "LionsCastles", "https://www.lionscastles.pt", "eGO platform — LionsCastles", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000008"), null, "habifit", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Habifit", "Habifit", "https://www.habifit.pt", "eGO platform — Habifit", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000009"), null, "cosy-imobiliaria", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Cosy Imobiliária", "Cosy Imobiliária", "https://www.cosyimobiliaria.pt", "eGO platform — Cosy Imobiliária", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000010"), null, "moderno-imoveis", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Moderno Imóveis", "Moderno Imóveis", "https://www.modernoimoveis.pt", "WordPress — Moderno Imóveis", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000011"), null, "neves-terlouw", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Neves & Terlouw", "Neves & Terlouw", "https://www.nevesterlouw.com", "Custom site — Neves & Terlouw", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000012"), null, "veigas", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Veigas", "Veigas", "https://www.veigas.eu", "Next.js — Veigas Imobiliária", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) },
                    { new Guid("b1000000-0000-0000-0000-000000000013"), null, "zome", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), true, new TimeSpan(0, 6, 0, 0, 0), "Zome", "Zome", "https://www.zome.pt/pt/leiria-h40157/imoveis", "Nuxt/Vue — Zome Leiria district", new DateTimeOffset(new DateTime(2026, 6, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)) }
                });

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

            migrationBuilder.CreateIndex(
                name: "ix_scraper_source_agency_id",
                table: "scraper_source",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_scraper_source_enabled",
                table: "scraper_source",
                column: "enabled");

            migrationBuilder.CreateIndex(
                name: "ix_scraper_source_scraper_key",
                table: "scraper_source",
                column: "scraper_key",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_agency_agency_id",
                table: "listing",
                column: "agency_id",
                principalTable: "agency",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_locations_location_id",
                table: "listing",
                column: "location_id",
                principalTable: "location",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_feature_listing_listing_id",
                table: "listing_feature",
                column: "listing_id",
                principalTable: "listing",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_image_listing_listing_id",
                table: "listing_image",
                column: "listing_id",
                principalTable: "listing",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_scrape_log_agency_agency_id",
                table: "scrape_log",
                column: "agency_id",
                principalTable: "agency",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listing_agency_agency_id",
                table: "listing");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_locations_location_id",
                table: "listing");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_feature_listing_listing_id",
                table: "listing_feature");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_image_listing_listing_id",
                table: "listing_image");

            migrationBuilder.DropForeignKey(
                name: "fk_scrape_log_agency_agency_id",
                table: "scrape_log");

            migrationBuilder.DropTable(
                name: "scrape_listing_change");

            migrationBuilder.DropTable(
                name: "scraper_source");

            migrationBuilder.DropPrimaryKey(
                name: "pk_scrape_log",
                table: "scrape_log");

            migrationBuilder.DropPrimaryKey(
                name: "pk_location",
                table: "location");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing_image",
                table: "listing_image");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing_feature",
                table: "listing_feature");

            migrationBuilder.DropPrimaryKey(
                name: "pk_listing",
                table: "listing");

            migrationBuilder.DropPrimaryKey(
                name: "pk_agency",
                table: "agency");

            migrationBuilder.RenameTable(
                name: "scrape_log",
                newName: "scrape_logs");

            migrationBuilder.RenameTable(
                name: "location",
                newName: "locations");

            migrationBuilder.RenameTable(
                name: "listing_image",
                newName: "listing_images");

            migrationBuilder.RenameTable(
                name: "listing_feature",
                newName: "listing_features");

            migrationBuilder.RenameTable(
                name: "listing",
                newName: "listings");

            migrationBuilder.RenameTable(
                name: "agency",
                newName: "agencies");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_log_status",
                table: "scrape_logs",
                newName: "ix_scrape_logs_status");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_log_started_at",
                table: "scrape_logs",
                newName: "ix_scrape_logs_started_at");

            migrationBuilder.RenameIndex(
                name: "ix_scrape_log_agency_id",
                table: "scrape_logs",
                newName: "ix_scrape_logs_agency_id");

            migrationBuilder.RenameIndex(
                name: "ix_location_municipality",
                table: "locations",
                newName: "ix_locations_municipality");

            migrationBuilder.RenameIndex(
                name: "ix_location_coordinate",
                table: "locations",
                newName: "ix_locations_coordinate");

            migrationBuilder.RenameIndex(
                name: "ix_listing_image_listing_id",
                table: "listing_images",
                newName: "ix_listing_images_listing_id");

            migrationBuilder.RenameIndex(
                name: "ix_listing_feature_listing_id",
                table: "listing_features",
                newName: "ix_listing_features_listing_id");

            migrationBuilder.RenameIndex(
                name: "ix_listing_status",
                table: "listings",
                newName: "ix_listings_status");

            migrationBuilder.RenameIndex(
                name: "ix_listing_property_type",
                table: "listings",
                newName: "ix_listings_property_type");

            migrationBuilder.RenameIndex(
                name: "ix_listing_price",
                table: "listings",
                newName: "ix_listings_price");

            migrationBuilder.RenameIndex(
                name: "ix_listing_location_id",
                table: "listings",
                newName: "ix_listings_location_id");

            migrationBuilder.RenameIndex(
                name: "ix_listing_city",
                table: "listings",
                newName: "ix_listings_city");

            migrationBuilder.RenameIndex(
                name: "ix_listing_agency_id_external_id",
                table: "listings",
                newName: "ix_listings_agency_id_external_id");

            migrationBuilder.RenameIndex(
                name: "ix_agency_slug",
                table: "agencies",
                newName: "ix_agencies_slug");

            migrationBuilder.AddPrimaryKey(
                name: "pk_scrape_logs",
                table: "scrape_logs",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_locations",
                table: "locations",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing_images",
                table: "listing_images",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listing_features",
                table: "listing_features",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_listings",
                table: "listings",
                column: "id");

            migrationBuilder.AddPrimaryKey(
                name: "pk_agencies",
                table: "agencies",
                column: "id");

            migrationBuilder.AddForeignKey(
                name: "fk_listing_features_listings_listing_id",
                table: "listing_features",
                column: "listing_id",
                principalTable: "listings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_images_listings_listing_id",
                table: "listing_images",
                column: "listing_id",
                principalTable: "listings",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_listings_agencies_agency_id",
                table: "listings",
                column: "agency_id",
                principalTable: "agencies",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_listings_locations_location_id",
                table: "listings",
                column: "location_id",
                principalTable: "locations",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_scrape_logs_agencies_agency_id",
                table: "scrape_logs",
                column: "agency_id",
                principalTable: "agencies",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
