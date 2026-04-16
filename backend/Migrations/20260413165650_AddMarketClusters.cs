using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMarketClusters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MarketClusters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClusterKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Label = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    PainCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NormalizedTechStack = table.Column<string>(type: "character varying(400)", maxLength: 400, nullable: false),
                    TechKeyPart = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Industry = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    CompanyType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    JobCount = table.Column<int>(type: "integer", nullable: false),
                    DirectClientCount = table.Column<int>(type: "integer", nullable: false),
                    DirectClientRatio = table.Column<double>(type: "double precision", nullable: false),
                    AvgOpportunityScore = table.Column<double>(type: "double precision", nullable: false),
                    AvgUrgencyScore = table.Column<double>(type: "double precision", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    BuyingPowerScore = table.Column<double>(type: "double precision", nullable: false),
                    PainSpecificityScore = table.Column<double>(type: "double precision", nullable: false),
                    EaseOfSaleScore = table.Column<double>(type: "double precision", nullable: false),
                    BlueOceanScore = table.Column<double>(type: "double precision", nullable: false),
                    RoiRank = table.Column<int>(type: "integer", nullable: false),
                    OpportunityType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsActionable = table.Column<bool>(type: "boolean", nullable: false),
                    RecommendedStrategy = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PriorityScore = table.Column<int>(type: "integer", nullable: false),
                    SynthesizedPain = table.Column<string>(type: "text", nullable: true),
                    SynthesizedMvp = table.Column<string>(type: "text", nullable: true),
                    SynthesizedLeadMessage = table.Column<string>(type: "text", nullable: true),
                    MvpType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    EstimatedBuildDays = table.Column<int>(type: "integer", nullable: true),
                    EstimatedDealSizeUsd = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LlmStatus = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketClusters", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_BlueOceanScore",
                table: "MarketClusters",
                column: "BlueOceanScore");

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_ClusterKey",
                table: "MarketClusters",
                column: "ClusterKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_IsActionable_PriorityScore",
                table: "MarketClusters",
                columns: new[] { "IsActionable", "PriorityScore" });

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_LastUpdatedAt",
                table: "MarketClusters",
                column: "LastUpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_LlmStatus",
                table: "MarketClusters",
                column: "LlmStatus");

            migrationBuilder.CreateIndex(
                name: "IX_MarketClusters_PainCategory_Industry_CompanyType",
                table: "MarketClusters",
                columns: new[] { "PainCategory", "Industry", "CompanyType" });

            migrationBuilder.AddForeignKey(
                name: "FK_JobInsights_MarketClusters_ClusterId",
                table: "JobInsights",
                column: "ClusterId",
                principalTable: "MarketClusters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobInsights_MarketClusters_ClusterId",
                table: "JobInsights");

            migrationBuilder.DropTable(
                name: "MarketClusters");
        }
    }
}
