using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SalesMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAt",
                table: "Orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "CostIsEstimated",
                table: "OrderItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "OrderItems",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            // Original order items did not capture costs. Freeze current variant costs
            // as labelled estimates rather than changing historical profit on each edit.
            migrationBuilder.Sql("""
                UPDATE "OrderItems" AS item
                SET "UnitCost" = variant."CostPrice", "CostIsEstimated" = TRUE
                FROM "ProductVariants" AS variant
                WHERE item."ProductVariantId" = variant."Id";

                UPDATE "Orders"
                SET "DeliveredAt" = COALESCE("UpdatedAt", "CreatedAt")
                WHERE "Status" = 3;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Orders_Status_DeliveredAt",
                table: "Orders",
                columns: new[] { "Status", "DeliveredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_Status_DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAt",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CostIsEstimated",
                table: "OrderItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "OrderItems");
        }
    }
}
