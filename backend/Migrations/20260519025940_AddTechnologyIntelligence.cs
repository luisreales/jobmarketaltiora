using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddTechnologyIntelligence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Technologies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Category = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FirstSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TotalMentions = table.Column<int>(type: "integer", nullable: false),
                    WeeklyMentions = table.Column<int>(type: "integer", nullable: false),
                    GrowthRate = table.Column<double>(type: "double precision", nullable: false),
                    MomentumScore = table.Column<double>(type: "double precision", nullable: false),
                    DemandScore = table.Column<double>(type: "double precision", nullable: false),
                    CompetitionScore = table.Column<double>(type: "double precision", nullable: false),
                    OpportunityScore = table.Column<double>(type: "double precision", nullable: false),
                    AvgLeadScore = table.Column<double>(type: "double precision", nullable: false),
                    AvgUrgency = table.Column<double>(type: "double precision", nullable: false),
                    IndustryCoverageCount = table.Column<int>(type: "integer", nullable: false),
                    ClusterCoverageCount = table.Column<int>(type: "integer", nullable: false),
                    EmergingScore = table.Column<double>(type: "double precision", nullable: false),
                    IsAiRelated = table.Column<bool>(type: "boolean", nullable: false),
                    IsCloudRelated = table.Column<bool>(type: "boolean", nullable: false),
                    IsLegacy = table.Column<bool>(type: "boolean", nullable: false),
                    LifecycleStage = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Technologies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TechnologyRelationships",
                columns: table => new
                {
                    SourceTechnologyId = table.Column<int>(type: "integer", nullable: false),
                    TargetTechnologyId = table.Column<int>(type: "integer", nullable: false),
                    CoOccurrenceCount = table.Column<int>(type: "integer", nullable: false),
                    CorrelationScore = table.Column<double>(type: "double precision", nullable: false),
                    IndustryAffinity = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    OpportunityAffinity = table.Column<double>(type: "double precision", nullable: false),
                    AiAffinity = table.Column<bool>(type: "boolean", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyRelationships", x => new { x.SourceTechnologyId, x.TargetTechnologyId });
                    table.ForeignKey(
                        name: "FK_TechnologyRelationships_Technologies_SourceTechnologyId",
                        column: x => x.SourceTechnologyId,
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TechnologyRelationships_Technologies_TargetTechnologyId",
                        column: x => x.TargetTechnologyId,
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TechnologyTrendSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TechnologyId = table.Column<int>(type: "integer", nullable: false),
                    SnapshotWeek = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MentionCount = table.Column<int>(type: "integer", nullable: false),
                    UniqueJobCount = table.Column<int>(type: "integer", nullable: false),
                    AvgOpportunityScore = table.Column<double>(type: "double precision", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TechnologyTrendSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TechnologyTrendSnapshots_Technologies_TechnologyId",
                        column: x => x.TechnologyId,
                        principalTable: "Technologies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_Category",
                table: "Technologies",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_IsAiRelated",
                table: "Technologies",
                column: "IsAiRelated");

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_LifecycleStage",
                table: "Technologies",
                column: "LifecycleStage");

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_MomentumScore",
                table: "Technologies",
                column: "MomentumScore");

            migrationBuilder.CreateIndex(
                name: "IX_Technologies_Name",
                table: "Technologies",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyRelationships_TargetTechnologyId",
                table: "TechnologyRelationships",
                column: "TargetTechnologyId");

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyTrendSnapshots_SnapshotWeek",
                table: "TechnologyTrendSnapshots",
                column: "SnapshotWeek");

            migrationBuilder.CreateIndex(
                name: "IX_TechnologyTrendSnapshots_TechnologyId_SnapshotWeek",
                table: "TechnologyTrendSnapshots",
                columns: new[] { "TechnologyId", "SnapshotWeek" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TechnologyRelationships");

            migrationBuilder.DropTable(
                name: "TechnologyTrendSnapshots");

            migrationBuilder.DropTable(
                name: "Technologies");
        }
    }
}
