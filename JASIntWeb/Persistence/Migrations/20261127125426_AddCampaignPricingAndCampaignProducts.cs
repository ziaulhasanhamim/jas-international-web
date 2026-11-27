using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261127125426_AddCampaignPricingAndCampaignProducts : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BadgeText",
            table: "Campaigns",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CouponId",
            table: "Campaigns",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DefaultDiscountType",
            table: "Campaigns",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "DefaultDiscountValue",
            table: "Campaigns",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "DiscountScenario",
            table: "Campaigns",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<decimal>(
            name: "MaxDiscountAmount",
            table: "Campaigns",
            type: "numeric",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "CampaignProducts",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CampaignId = table.Column<int>(type: "integer", nullable: false),
                ProductId = table.Column<int>(type: "integer", nullable: false),
                ProductVariantId = table.Column<int>(type: "integer", nullable: true),
                DiscountType = table.Column<int>(type: "integer", nullable: false),
                DiscountValue = table.Column<decimal>(type: "numeric", nullable: false),
                MaxDiscountAmount = table.Column<decimal>(type: "numeric", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CampaignProducts", x => x.Id);
                table.ForeignKey(
                    name: "FK_CampaignProducts_Campaigns_CampaignId",
                    column: x => x.CampaignId,
                    principalTable: "Campaigns",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CampaignProducts_ProductVariants_ProductVariantId",
                    column: x => x.ProductVariantId,
                    principalTable: "ProductVariants",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CampaignProducts_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_CouponId",
            table: "Campaigns",
            column: "CouponId");

        migrationBuilder.CreateIndex(
            name: "IX_CampaignProducts_CampaignId_ProductId",
            table: "CampaignProducts",
            columns: new[] { "CampaignId", "ProductId" },
            unique: true,
            filter: "\"ProductVariantId\" IS NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CampaignProducts_CampaignId_ProductVariantId",
            table: "CampaignProducts",
            columns: new[] { "CampaignId", "ProductVariantId" },
            unique: true,
            filter: "\"ProductVariantId\" IS NOT NULL");

        migrationBuilder.CreateIndex(
            name: "IX_CampaignProducts_CampaignId_SortOrder",
            table: "CampaignProducts",
            columns: new[] { "CampaignId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_CampaignProducts_ProductId",
            table: "CampaignProducts",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_CampaignProducts_ProductVariantId",
            table: "CampaignProducts",
            column: "ProductVariantId");

        migrationBuilder.AddForeignKey(
            name: "FK_Campaigns_Coupons_CouponId",
            table: "Campaigns",
            column: "CouponId",
            principalTable: "Coupons",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Campaigns_Coupons_CouponId",
            table: "Campaigns");

        migrationBuilder.DropTable(
            name: "CampaignProducts");

        migrationBuilder.DropIndex(
            name: "IX_Campaigns_CouponId",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "BadgeText",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "CouponId",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "DefaultDiscountType",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "DefaultDiscountValue",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "DiscountScenario",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "MaxDiscountAmount",
            table: "Campaigns");
    }
}
