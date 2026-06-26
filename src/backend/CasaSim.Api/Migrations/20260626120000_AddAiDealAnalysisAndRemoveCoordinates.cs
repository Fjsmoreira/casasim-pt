using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaSim.Api.Migrations;

public partial class AddAiDealAnalysisAndRemoveCoordinates : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "corrected_facts_json",
            table: "listing_ai_enrichment",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "correction_audit_json",
            table: "listing_ai_enrichment",
            type: "jsonb",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "deal_label",
            table: "listing_ai_enrichment",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.DropIndex(
            name: "ix_location_coordinate",
            table: "location");

        migrationBuilder.DropColumn(
            name: "coordinate",
            table: "location");

        migrationBuilder.DropColumn(
            name: "geohash",
            table: "location");

        migrationBuilder.DropColumn(
            name: "latitude",
            table: "location");

        migrationBuilder.DropColumn(
            name: "longitude",
            table: "location");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<NetTopologySuite.Geometries.Point>(
            name: "coordinate",
            table: "location",
            type: "geometry(Point, 4326)",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "geohash",
            table: "location",
            type: "character varying(20)",
            maxLength: 20,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "latitude",
            table: "location",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "longitude",
            table: "location",
            type: "numeric(10,7)",
            precision: 10,
            scale: 7,
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "ix_location_coordinate",
            table: "location",
            column: "coordinate")
            .Annotation("Npgsql:IndexMethod", "GIST");

        migrationBuilder.DropColumn(
            name: "corrected_facts_json",
            table: "listing_ai_enrichment");

        migrationBuilder.DropColumn(
            name: "correction_audit_json",
            table: "listing_ai_enrichment");

        migrationBuilder.DropColumn(
            name: "deal_label",
            table: "listing_ai_enrichment");
    }
}
