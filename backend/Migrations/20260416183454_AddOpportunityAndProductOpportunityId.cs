using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityAndProductOpportunityId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "OpportunityId",
                table: "ProductSuggestions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Opportunities",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    Company = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    JobTitle = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    JobDescription = table.Column<string>(type: "text", nullable: true),
                    TechStack = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ProductIdeasJson = table.Column<string>(type: "text", nullable: true),
                    LlmStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "pending"),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SynthesizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Opportunities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Opportunities_JobOffers_JobId",
                        column: x => x.JobId,
                        principalTable: "JobOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProductSuggestions_OpportunityId",
                table: "ProductSuggestions",
                column: "OpportunityId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_CreatedAt",
                table: "Opportunities",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_JobId",
                table: "Opportunities",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_Opportunities_LlmStatus",
                table: "Opportunities",
                column: "LlmStatus");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductSuggestions_Opportunities_OpportunityId",
                table: "ProductSuggestions",
                column: "OpportunityId",
                principalTable: "Opportunities",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductSuggestions_Opportunities_OpportunityId",
                table: "ProductSuggestions");

            migrationBuilder.DropTable(
                name: "Opportunities");

            migrationBuilder.DropIndex(
                name: "IX_ProductSuggestions_OpportunityId",
                table: "ProductSuggestions");

            migrationBuilder.DropColumn(
                name: "OpportunityId",
                table: "ProductSuggestions");
        }
    }
}
