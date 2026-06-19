using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CasaSim.Api.Migrations
{
    /// <inheritdoc />
    public partial class UpgradeToNet10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_listing_agency_agency_id",
                table: "listing");

            migrationBuilder.DropForeignKey(
                name: "fk_listing_location_location_id",
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

            migrationBuilder.DropIndex(
                name: "ix_listing_agency_id",
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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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

            migrationBuilder.CreateIndex(
                name: "ix_listing_agency_id",
                table: "listing",
                column: "agency_id");

            migrationBuilder.AddForeignKey(
                name: "fk_listing_agency_agency_id",
                table: "listing",
                column: "agency_id",
                principalTable: "agency",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_listing_location_location_id",
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
    }
}
