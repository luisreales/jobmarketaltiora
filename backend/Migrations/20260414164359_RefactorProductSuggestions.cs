using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class RefactorProductSuggestions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Remove duplicate ProductName rows before creating the unique index.
            // Keep the row with the highest Id per ProductName.
            migrationBuilder.Sql(
                """
                DELETE FROM "ProductSuggestions"
                WHERE "Id" NOT IN (
                    SELECT MAX("Id") FROM "ProductSuggestions" GROUP BY "ProductName"
                );
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_ProductSuggestions_MarketClusters_ClusterId",
                table: "ProductSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_ClusterId",
                table: "ProductSuggestions");

            migrationBuilder.RenameColumn(
                name: "ClusterId",
                table: "ProductSuggestions",
                newName: "TotalJobCount");

            migrationBuilder.AlterColumn<string>(
                name: "WhyNow",
                table: "ProductSuggestions",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);

            migrationBuilder.AlterColumn<string>(
                name: "TechFocus",
                table: "ProductSuggestions",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(200)",
                oldMaxLength: 200);

            migrationBuilder.AddColumn<double>(
                name: "AvgDirectClientRatio",
                table: "ProductSuggestions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "AvgUrgencyScore",
                table: "ProductSuggestions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "ClusterCount",
                table: "ProductSuggestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "ClusterIdsJson",
                table: "ProductSuggestions",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "ProductSuggestions",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "LlmStatus",
                table: "ProductSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "pending");

            migrationBuilder.AddColumn<string>(
                name: "SynthesisDetailJson",
                table: "ProductSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TopBlueOceanScore",
                table: "ProductSuggestions",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_OpportunityType",
                table: "ProductSuggestions",
                column: "OpportunityType");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_ProductName",
                table: "ProductSuggestions",
                column: "ProductName",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_OpportunityType",
                table: "ProductSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_ProductName",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "AvgDirectClientRatio",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "AvgUrgencyScore",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "ClusterCount",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "ClusterIdsJson",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "LlmStatus",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "SynthesisDetailJson",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "TopBlueOceanScore",
                table: "ProductSuggestions");

            migrationBuilder.RenameColumn(
                name: "TotalJobCount",
                table: "ProductSuggestions",
                newName: "ClusterId");

            migrationBuilder.AlterColumn<string>(
                name: "WhyNow",
                table: "ProductSuggestions",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(500)",
                oldMaxLength: 500);

            migrationBuilder.AlterColumn<string>(
                name: "TechFocus",
                table: "ProductSuggestions",
                type: "character varying(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(400)",
                oldMaxLength: 400);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_ClusterId",
                table: "ProductSuggestions",
                column: "ClusterId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSuggestions_MarketClusters_ClusterId",
                table: "ProductSuggestions",
                column: "ClusterId",
                principalTable: "MarketClusters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
