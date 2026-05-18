using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class EnhanceIdeaVault : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_JobOffers_JobId",
                table: "Opportunities");

            migrationBuilder.AddColumn<int>(
                name: "AppSumoProductId",
                table: "OpportunityIdeas",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Source",
                table: "OpportunityIdeas",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "LinkedIn");

            migrationBuilder.AlterColumn<int>(
                name: "JobId",
                table: "Opportunities",
                type: "integer",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "integer");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityIdeas_AppSumoProductId",
                table: "OpportunityIdeas",
                column: "AppSumoProductId");

            migrationBuilder.CreateIndex(
                name: "IX_OpportunityIdeas_Source",
                table: "OpportunityIdeas",
                column: "Source");

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_JobOffers_JobId",
                table: "Opportunities",
                column: "JobId",
                principalTable: "JobOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_OpportunityIdeas_AppSumoProducts_AppSumoProductId",
                table: "OpportunityIdeas",
                column: "AppSumoProductId",
                principalTable: "AppSumoProducts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Opportunities_JobOffers_JobId",
                table: "Opportunities");

            migrationBuilder.DropForeignKey(
                name: "FK_OpportunityIdeas_AppSumoProducts_AppSumoProductId",
                table: "OpportunityIdeas");

            migrationBuilder.DropIndex(
                name: "IX_OpportunityIdeas_AppSumoProductId",
                table: "OpportunityIdeas");

            migrationBuilder.DropIndex(
                name: "IX_OpportunityIdeas_Source",
                table: "OpportunityIdeas");

            migrationBuilder.DropColumn(
                name: "AppSumoProductId",
                table: "OpportunityIdeas");

            migrationBuilder.DropColumn(
                name: "Source",
                table: "OpportunityIdeas");

            migrationBuilder.AlterColumn<int>(
                name: "JobId",
                table: "Opportunities",
                type: "integer",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "integer",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Opportunities_JobOffers_JobId",
                table: "Opportunities",
                column: "JobId",
                principalTable: "JobOffers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
