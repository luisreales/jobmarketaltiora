using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddAppSumoScraper : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AppSumoCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    ParentSlug = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ScrapedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSumoCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSumoScrapeRuns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    FinishedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Running"),
                    ProductsScraped = table.Column<int>(type: "integer", nullable: false),
                    ReviewsSaved = table.Column<int>(type: "integer", nullable: false),
                    ErrorCount = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSumoScrapeRuns", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AppSumoProducts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CategoryId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Slug = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    OverallRating = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: true),
                    TotalReviewCount = table.Column<int>(type: "integer", nullable: true),
                    PricingModel = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TagsJson = table.Column<string>(type: "text", nullable: true),
                    ScrapedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSumoProducts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSumoProducts_AppSumoCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "AppSumoCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AppSumoReviews",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    AppSumoReviewId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    TacoRating = table.Column<byte>(type: "smallint", nullable: false),
                    ReviewerName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ReviewDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ReviewText = table.Column<string>(type: "text", nullable: false),
                    FoundHelpful = table.Column<int>(type: "integer", nullable: true),
                    IsVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppSumoReviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppSumoReviews_AppSumoProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppSumoProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductScrapeStates",
                columns: table => new
                {
                    ProductId = table.Column<int>(type: "integer", nullable: false),
                    LastRunId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false, defaultValue: "Pending"),
                    AttemptCount = table.Column<byte>(type: "smallint", nullable: false),
                    LastError = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductScrapeStates", x => x.ProductId);
                    table.ForeignKey(
                        name: "FK_ProductScrapeStates_AppSumoProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "AppSumoProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductScrapeStates_AppSumoScrapeRuns_LastRunId",
                        column: x => x.LastRunId,
                        principalTable: "AppSumoScrapeRuns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoCategories_Slug",
                table: "AppSumoCategories",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoProducts_CategoryId",
                table: "AppSumoProducts",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoProducts_Slug",
                table: "AppSumoProducts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoReviews_ProductId",
                table: "AppSumoReviews",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoReviews_ProductId_AppSumoReviewId",
                table: "AppSumoReviews",
                columns: new[] { "ProductId", "AppSumoReviewId" },
                unique: true,
                filter: "\"AppSumoReviewId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoReviews_ReviewDate",
                table: "AppSumoReviews",
                column: "ReviewDate");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoReviews_TacoRating",
                table: "AppSumoReviews",
                column: "TacoRating");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoScrapeRuns_StartedAt",
                table: "AppSumoScrapeRuns",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppSumoScrapeRuns_Status",
                table: "AppSumoScrapeRuns",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_ProductScrapeStates_LastRunId",
                table: "ProductScrapeStates",
                column: "LastRunId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppSumoReviews");

            migrationBuilder.DropTable(
                name: "ProductScrapeStates");

            migrationBuilder.DropTable(
                name: "AppSumoProducts");

            migrationBuilder.DropTable(
                name: "AppSumoScrapeRuns");

            migrationBuilder.DropTable(
                name: "AppSumoCategories");
        }
    }
}
