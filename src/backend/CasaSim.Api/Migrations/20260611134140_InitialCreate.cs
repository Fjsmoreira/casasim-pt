using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable enable

namespace CasaSim.Api.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostGIS extension for spatial queries
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS postgis", suppressTransaction: true);

            // ---------- Agency ----------
            migrationBuilder.CreateTable(
                name: "agency",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    slug = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    website_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    contact_email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    contact_phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_agency", x => x.id);
                });

            // ---------- Location ----------
            migrationBuilder.CreateTable(
                name: "location",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    address_line1 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    parish = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    municipality = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    district = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    country_code = table.Column<string>(type: "character varying(5)", maxLength: 5, nullable: false),
                    postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    latitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    longitude = table.Column<decimal>(type: "numeric(10,7)", precision: 10, scale: 7, nullable: true),
                    geohash = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    raw_address = table.Column<string>(type: "text", nullable: true),
                    coordinate = table.Column<Point>(type: "geometry(Point, 4326)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_location", x => x.id);
                });

            // ---------- Listing ----------
            migrationBuilder.CreateTable(
                name: "listing",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location_id = table.Column<Guid>(type: "uuid", nullable: true),
                    external_id = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    canonical_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    title = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    property_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    price = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: true),
                    currency = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    area_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    land_area_m2 = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    bedrooms = table.Column<int>(type: "integer", nullable: true),
                    bathrooms = table.Column<int>(type: "integer", nullable: true),
                    parking_spaces = table.Column<int>(type: "integer", nullable: true),
                    year_built = table.Column<int>(type: "integer", nullable: true),
                    energy_class = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_agency_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agency",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_listing_location_location_id",
                        column: x => x.location_id,
                        principalTable: "location",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ---------- ListingImage ----------
            migrationBuilder.CreateTable(
                name: "listing_image",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    thumbnail_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    alt_text = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_primary = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_image", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_image_listing_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listing",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- ListingFeature ----------
            migrationBuilder.CreateTable(
                name: "listing_feature",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    listing_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    unit = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_listing_feature", x => x.id);
                    table.ForeignKey(
                        name: "fk_listing_feature_listing_listing_id",
                        column: x => x.listing_id,
                        principalTable: "listing",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // ---------- ScrapeLog ----------
            migrationBuilder.CreateTable(
                name: "scrape_log",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    agency_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    listings_found = table.Column<int>(type: "integer", nullable: false),
                    listings_created = table.Column<int>(type: "integer", nullable: false),
                    listings_updated = table.Column<int>(type: "integer", nullable: false),
                    listings_removed = table.Column<int>(type: "integer", nullable: false),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    error_details = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    first_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scrape_log", x => x.id);
                    table.ForeignKey(
                        name: "fk_scrape_log_agency_agency_id",
                        column: x => x.agency_id,
                        principalTable: "agency",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            // ========== INDEXES ==========

            // Agency unique slug
            migrationBuilder.CreateIndex(
                name: "ix_agency_slug",
                table: "agency",
                column: "slug",
                unique: true);

            // Listing indexes
            migrationBuilder.CreateIndex(
                name: "ix_listing_agency_id_external_id",
                table: "listing",
                columns: new[] { "agency_id", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_listing_city",
                table: "listing",
                column: "city");

            migrationBuilder.CreateIndex(
                name: "ix_listing_price",
                table: "listing",
                column: "price");

            migrationBuilder.CreateIndex(
                name: "ix_listing_property_type",
                table: "listing",
                column: "property_type");

            migrationBuilder.CreateIndex(
                name: "ix_listing_status",
                table: "listing",
                column: "status");

            // Location indexes
            migrationBuilder.CreateIndex(
                name: "ix_location_coordinate",
                table: "location",
                column: "coordinate")
                .Annotation("Npgsql:IndexMethod", "GIST");

            migrationBuilder.CreateIndex(
                name: "ix_location_municipality",
                table: "location",
                column: "municipality");

            // ListingImage FK
            migrationBuilder.CreateIndex(
                name: "ix_listing_image_listing_id",
                table: "listing_image",
                column: "listing_id");

            // ListingFeature FK
            migrationBuilder.CreateIndex(
                name: "ix_listing_feature_listing_id",
                table: "listing_feature",
                column: "listing_id");

            // ScrapeLog indexes
            migrationBuilder.CreateIndex(
                name: "ix_scrape_log_agency_id",
                table: "scrape_log",
                column: "agency_id");

            migrationBuilder.CreateIndex(
                name: "ix_scrape_log_started_at",
                table: "scrape_log",
                column: "started_at");

            migrationBuilder.CreateIndex(
                name: "ix_scrape_log_status",
                table: "scrape_log",
                column: "status");

            // Seed agency data
            var seedingNow = new DateTimeOffset(2026, 6, 11, 0, 0, 0, TimeSpan.Zero);
            migrationBuilder.InsertData(
                table: "agency",
                columns: new[] { "id", "name", "slug", "website_url", "is_active", "created_at", "updated_at" },
                values: new object[,]
                {
                    { Guid.Parse("a1000000-0000-0000-0000-000000000001"), "Remax Pombal", "remax-pombal", "https://www.remax.pt", true, seedingNow, seedingNow },
                    { Guid.Parse("a1000000-0000-0000-0000-000000000002"), "Century21 Pombal", "century21-pombal", "https://www.century21.pt", true, seedingNow, seedingNow },
                    { Guid.Parse("a1000000-0000-0000-0000-000000000003"), "ERA Pombal", "era-pombal", "https://www.era.pt", true, seedingNow, seedingNow },
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "listing_image");
            migrationBuilder.DropTable(name: "listing_feature");
            migrationBuilder.DropTable(name: "scrape_log");
            migrationBuilder.DropTable(name: "listing");
            migrationBuilder.DropTable(name: "location");
            migrationBuilder.DropTable(name: "agency");
        }
    }
}
