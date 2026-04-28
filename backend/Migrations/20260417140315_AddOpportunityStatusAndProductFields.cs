using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityStatusAndProductFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProductSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "open");

            migrationBuilder.AddColumn<string>(
                name: "TechnicalMvpJson",
                table: "ProductSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "Opportunities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_Status",
                table: "ProductSuggestions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_Status",
                table: "Opportunities",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_Status",
                table: "ProductSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_Opportunities_Status",
                table: "Opportunities");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "TechnicalMvpJson",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Opportunities");
        }
    }
}
