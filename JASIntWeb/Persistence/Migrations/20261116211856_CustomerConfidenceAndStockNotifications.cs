using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261116211856_CustomerConfidenceAndStockNotifications : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "MarketingConsent",
            table: "Orders",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.CreateTable(
            name: "StockNotifications",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                ProductVariantId = table.Column<int>(type: "integer", nullable: false),
                Email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                NormalizedEmail = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                Token = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<int>(type: "integer", nullable: false),
                ConfirmedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ConfirmationSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                NotificationSentAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                NextAttemptAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                LeaseUntil = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                LeaseToken = table.Column<Guid>(type: "uuid", nullable: true),
                Attempts = table.Column<int>(type: "integer", nullable: false),
                LastError = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_StockNotifications", x => x.Id);
                table.ForeignKey(
                    name: "FK_StockNotifications_ProductVariants_ProductVariantId",
                    column: x => x.ProductVariantId,
                    principalTable: "ProductVariants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_StockNotifications_ProductVariantId_NormalizedEmail",
            table: "StockNotifications",
            columns: new[] { "ProductVariantId", "NormalizedEmail" },
            unique: true,
            filter: "\"Status\" IN (0, 1, 4)");

        migrationBuilder.CreateIndex(
            name: "IX_StockNotifications_Status_NextAttemptAt",
            table: "StockNotifications",
            columns: new[] { "Status", "NextAttemptAt" });

        migrationBuilder.CreateIndex(
            name: "IX_StockNotifications_Token",
            table: "StockNotifications",
            column: "Token",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "StockNotifications");

        migrationBuilder.DropColumn(
            name: "MarketingConsent",
            table: "Orders");
    }
}
