using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddJobInsights : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobOffers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ExternalId = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Company = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Location = table.Column<string>(type: "character varying(220)", maxLength: 220, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "character varying(600)", maxLength: 600, nullable: false),
                    Contact = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    SalaryRange = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PublishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Seniority = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    ContractType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    SearchTerm = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CapturedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MetadataJson = table.Column<string>(type: "jsonb", nullable: true),
                    Category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    OpportunityScore = table.Column<int>(type: "integer", nullable: false),
                    IsConsultingCompany = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobOffers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProviderSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Provider = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    IsAuthenticated = table.Column<bool>(type: "boolean", nullable: false),
                    Username = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastUsedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "JobInsights",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    JobId = table.Column<int>(type: "integer", nullable: false),
                    MainPainPoint = table.Column<string>(type: "character varying(180)", maxLength: 180, nullable: false),
                    PainCategory = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    PainDescription = table.Column<string>(type: "text", nullable: false),
                    TechStack = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IsDirectClient = table.Column<bool>(type: "boolean", nullable: false),
                    CompanyType = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    OpportunityScore = table.Column<int>(type: "integer", nullable: false),
                    UrgencyScore = table.Column<int>(type: "integer", nullable: false),
                    SuggestedSolution = table.Column<string>(type: "text", nullable: false),
                    LeadMessage = table.Column<string>(type: "text", nullable: false),
                    ConfidenceScore = table.Column<double>(type: "double precision", nullable: false),
                    DecisionSource = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    IsProcessed = table.Column<bool>(type: "boolean", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EngineVersion = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    RawModelResponse = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobInsights", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobInsights_JobOffers_JobId",
                        column: x => x.JobId,
                        principalTable: "JobOffers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_IsProcessed_ProcessedAt",
                table: "JobInsights",
                columns: new[] { "IsProcessed", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_JobId",
                table: "JobInsights",
                column: "JobId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_MainPainPoint_ProcessedAt",
                table: "JobInsights",
                columns: new[] { "MainPainPoint", "ProcessedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobInsights_Status",
                table: "JobInsights",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_CapturedAt",
                table: "JobOffers",
                column: "CapturedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_Company_Location",
                table: "JobOffers",
                columns: new[] { "Company", "Location" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_IsConsultingCompany_OpportunityScore",
                table: "JobOffers",
                columns: new[] { "IsConsultingCompany", "OpportunityScore" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_IsProcessed_CapturedAt",
                table: "JobOffers",
                columns: new[] { "IsProcessed", "CapturedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobOffers_Source_ExternalId",
                table: "JobOffers",
                columns: new[] { "Source", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderSessions_IsAuthenticated",
                table: "ProviderSessions",
                column: "IsAuthenticated");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderSessions_Provider",
                table: "ProviderSessions",
                column: "Provider",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobInsights");

            migrationBuilder.DropTable(
                name: "ProviderSessions");

            migrationBuilder.DropTable(
                name: "JobOffers");
        }
    }
}
