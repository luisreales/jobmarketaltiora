using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddClusterIdToAiPromptLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClusterId",
                table: "AiPromptLogs",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiPromptLogs_ClusterId",
                table: "AiPromptLogs",
                column: "ClusterId");

            migrationBuilder.AddForeignKey(
                name: "FK_AiPromptLogs_MarketClusters_ClusterId",
                table: "AiPromptLogs",
                column: "ClusterId",
                principalTable: "MarketClusters",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AiPromptLogs_MarketClusters_ClusterId",
                table: "AiPromptLogs");

            migrationBuilder.DropIndex(
                name: "IX_AiPromptLogs_ClusterId",
                table: "AiPromptLogs");

            migrationBuilder.DropColumn(
                name: "ClusterId",
                table: "AiPromptLogs");
        }
    }
}
