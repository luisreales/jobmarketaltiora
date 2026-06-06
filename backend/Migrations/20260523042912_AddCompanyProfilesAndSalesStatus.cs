using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddCompanyProfilesAndSalesStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClosedAt",
                table: "ProductSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ContactedAt",
                table: "ProductSuggestions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesNotes",
                table: "ProductSuggestions",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesStatus",
                table: "ProductSuggestions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "new");

            migrationBuilder.AddColumn<decimal>(
                name: "WonDealSizeUsd",
                table: "ProductSuggestions",
                type: "numeric(12,2)",
                precision: 12,
                scale: 2,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "CompanyProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompanyName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    NormalizedName = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    CompanyType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false, defaultValue: "Unknown"),
                    PrimaryIndustry = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false, defaultValue: "Unknown"),
                    TechStackJson = table.Column<string>(type: "text", nullable: false, defaultValue: "[]"),
                    TopPainCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    TotalJobCount = table.Column<int>(type: "integer", nullable: false),
                    AvgUrgencyScore = table.Column<double>(type: "double precision", nullable: false),
                    AvgOpportunityScore = table.Column<double>(type: "double precision", nullable: false),
                    AvgLeadScore = table.Column<double>(type: "double precision", nullable: false),
                    HiringVelocity = table.Column<double>(type: "double precision", nullable: false),
                    IsDirectClient = table.Column<bool>(type: "boolean", nullable: false),
                    HasAiInitiative = table.Column<bool>(type: "boolean", nullable: false),
                    HasCloudMigration = table.Column<bool>(type: "boolean", nullable: false),
                    ProspectScore = table.Column<double>(type: "double precision", nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CompanyProfiles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_SalesStatus",
                table: "ProductSuggestions",
                column: "SalesStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_HasAiInitiative",
                table: "CompanyProfiles",
                column: "HasAiInitiative");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_IsDirectClient",
                table: "CompanyProfiles",
                column: "IsDirectClient");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_NormalizedName",
                table: "CompanyProfiles",
                column: "NormalizedName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_PrimaryIndustry",
                table: "CompanyProfiles",
                column: "PrimaryIndustry");

            migrationBuilder.CreateIndex(
                name: "IX_CompanyProfiles_ProspectScore",
                table: "CompanyProfiles",
                column: "ProspectScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CompanyProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_SalesStatus",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "ClosedAt",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "ContactedAt",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "SalesNotes",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "SalesStatus",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "WonDealSizeUsd",
                table: "ProductSuggestions");
        }
    }
}
