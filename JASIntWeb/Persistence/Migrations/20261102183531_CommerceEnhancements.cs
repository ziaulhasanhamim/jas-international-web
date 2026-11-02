using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommerceEnhancements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CouponCode",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DiscountAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidAmount",
                table: "Orders",
                type: "numeric",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentReferenceCode",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SenderPhoneNumber",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TransactionId",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Division",
                table: "DeliveryAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Thana",
                table: "DeliveryAddresses",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Coupons",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "text", nullable: false),
                    DiscountType = table.Column<int>(type: "integer", nullable: false),
                    DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxDiscountAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    MinOrderAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    ExpiryDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    UsageLimit = table.Column<int>(type: "integer", nullable: true),
                    TimesUsed = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                    UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Coupons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeliverySettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HubDistrict = table.Column<string>(type: "text", nullable: false),
                    HubCity = table.Column<string>(type: "text", nullable: true),
                    HubCityFee = table.Column<decimal>(type: "numeric", nullable: false),
                    OutsideFee = table.Column<decimal>(type: "numeric", nullable: false),
                    FreeDeliveryThreshold = table.Column<decimal>(type: "numeric", nullable: true),
                    RequireAdvanceForCOD = table.Column<bool>(type: "boolean", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    BkashNumber = table.Column<string>(type: "text", nullable: false),
                    NagadNumber = table.Column<string>(type: "text", nullable: false),
                    BanglaQrImageUrl = table.Column<string>(type: "text", nullable: true),
                    PaymentInstructions = table.Column<string>(type: "text", nullable: true),
                    SmsWebhookSecret = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeliverySettings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_BrandId_CreatedAt",
                table: "Products",
                columns: new[] { "IsDeleted", "BrandId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Products_IsDeleted_CategoryId_CreatedAt",
                table: "Products",
                columns: new[] { "IsDeleted", "CategoryId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Orders_PaymentReferenceCode",
                table: "Orders",
                column: "PaymentReferenceCode");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_TransactionId",
                table: "Orders",
                column: "TransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_Code",
                table: "Coupons",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Coupons_IsActive_ExpiryDate",
                table: "Coupons",
                columns: new[] { "IsActive", "ExpiryDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Coupons");

            migrationBuilder.DropTable(
                name: "DeliverySettings");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted_BrandId_CreatedAt",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_IsDeleted_CategoryId_CreatedAt",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Orders_PaymentReferenceCode",
                table: "Orders");

            migrationBuilder.DropIndex(
                name: "IX_Orders_TransactionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CouponCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "DiscountAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaidAmount",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "PaymentReferenceCode",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "SenderPhoneNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "TransactionId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "Division",
                table: "DeliveryAddresses");

            migrationBuilder.DropColumn(
                name: "Thana",
                table: "DeliveryAddresses");
        }
    }
}
