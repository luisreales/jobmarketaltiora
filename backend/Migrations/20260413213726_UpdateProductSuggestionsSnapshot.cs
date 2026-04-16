using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProductSuggestionsSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProductSuggestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClusterId = table.Column<int>(type: "integer", nullable: false),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProductDescription = table.Column<string>(type: "text", nullable: false),
                    WhyNow = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    Offer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ActionToday = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    TechFocus = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    EstimatedBuildDays = table.Column<int>(type: "integer", nullable: false),
                    MinDealSizeUsd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    MaxDealSizeUsd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PriorityScore = table.Column<int>(type: "integer", nullable: false),
                    OpportunityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductSuggestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProductSuggestions_MarketClusters_ClusterId",
                        column: x => x.ClusterId,
                        principalTable: "MarketClusters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_ClusterId",
                table: "ProductSuggestions",
                column: "ClusterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_GeneratedAt",
                table: "ProductSuggestions",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_PriorityScore",
                table: "ProductSuggestions",
                column: "PriorityScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductSuggestions");
        }
    }
}
