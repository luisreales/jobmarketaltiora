using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace backend.Migrations
{
    /// <inheritdoc />
    public partial class AddOpportunityEngineV2Fields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "BuyingIntentScore",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "DeliveryFeasibility",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EnterpriseComplexity",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedCloseProbability",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedTam",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "HiringVelocity",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<int>(
                name: "PriorityScoreV2",
                table: "MarketClusters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RecommendedServiceModel",
                table: "MarketClusters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "RevenuePotential",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "SalesAngle",
                table: "MarketClusters",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "SalesFriction",
                table: "MarketClusters",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "WhyNow",
                table: "MarketClusters",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BuyingIntentScore",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "DeliveryFeasibility",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "EnterpriseComplexity",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "EstimatedCloseProbability",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "EstimatedTam",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "HiringVelocity",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "PriorityScoreV2",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "RecommendedServiceModel",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "RevenuePotential",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "SalesAngle",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "SalesFriction",
                table: "MarketClusters");

            migrationBuilder.DropColumn(
                name: "WhyNow",
                table: "MarketClusters");
        }
    }
}
