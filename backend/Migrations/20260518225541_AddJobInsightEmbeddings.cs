using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInsightEmbeddings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SemanticGroupKey",
                table: "MarketClusters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmbeddedAt",
                table: "JobInsights",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EmbeddingVectorJson",
                table: "JobInsights",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SemanticGroupKey",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "EmbeddedAt",
                table: "JobInsights");

            migrationBuilder.DropColumn(
                name: "EmbeddingVectorJson",
                table: "JobInsights");
        }
    }
}
