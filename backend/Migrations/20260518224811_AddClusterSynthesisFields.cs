using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddClusterSynthesisFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "LlmConfidence",
                table: "MarketClusters",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SynthesizedBusinessOpportunity",
                table: "MarketClusters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LlmConfidence",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "SynthesizedBusinessOpportunity",
                table: "MarketClusters");
        }
    }
}
