using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInsightDataQualityFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClusterId",
                table: "JobInsights",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Industry",
                table: "JobInsights",
                type: "character varying(60)",
                maxLength: 60,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<int>(
                name: "LeadScore",
                table: "JobInsights",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedTechStack",
                table: "JobInsights",
                type: "character varying(400)",
                maxLength: 400,
                nullable: false,
                defaultValue: "Unknown");

            migrationBuilder.AddColumn<string>(
                name: "TechTokensJson",
                table: "JobInsights",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_ClusterId",
                table: "JobInsights",
                column: "ClusterId");

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_Industry",
                table: "JobInsights",
                column: "Industry");

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_LeadScore",
                table: "JobInsights",
                column: "LeadScore");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_JobInsights_ClusterId",
                table: "JobInsights");

            migrationBuilder.DropIndex(
                name: "IX_JobInsights_Industry",
                table: "JobInsights");

            migrationBuilder.DropIndex(
                name: "IX_JobInsights_LeadScore",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "ClusterId",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "Industry",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "LeadScore",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "NormalizedTechStack",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "TechTokensJson",
                table: "JobInsights");
        }
    }
}
