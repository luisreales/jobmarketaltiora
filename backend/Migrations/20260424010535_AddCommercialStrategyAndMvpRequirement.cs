using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCommercialStrategyAndMvpRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommercialStrategies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyContext = table.Column<string>(type: "text", nullable: false),
                    RealBusinessProblem = table.Column<string>(type: "text", nullable: false),
                    FinancialImpact = table.Column<string>(type: "text", nullable: false),
                    MvpDefinition = table.Column<string>(type: "text", nullable: false),
                    TargetBuyer = table.Column<string>(type: "text", nullable: false),
                    PricingStrategy = table.Column<string>(type: "text", nullable: false),
                    OutreachMessage = table.Column<string>(type: "text", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommercialStrategies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CommercialStrategies_ProductSuggestions_ProductId",
                        column: x => x.ProductId,
                        principalTable: "ProductSuggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MvpRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: true),
                    ProductName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CompanyContext = table.Column<string>(type: "text", nullable: false),
                    ArchitectureStrategy = table.Column<string>(type: "text", nullable: false),
                    RequiredTechStackJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    EstimatedTimelines = table.Column<string>(type: "text", nullable: false),
                    CoreFeaturesJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MvpRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MvpRequirements_ProductSuggestions_ProductId",
                        column: x => x.ProductId,
                        principalTable: "ProductSuggestions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommercialStrategies_GeneratedAt",
                table: "CommercialStrategies",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_CommercialStrategies_ProductId",
                table: "CommercialStrategies",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_MvpRequirements_GeneratedAt",
                table: "MvpRequirements",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MvpRequirements_ProductId",
                table: "MvpRequirements",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CommercialStrategies");

            migrationBuilder.DropTable(
                name: "MvpRequirements");
        }
    }
}
