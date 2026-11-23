using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261123123925_AddProductTagsCouponRulesAndCampaigns : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<List<int>>(
            name: "ExcludedBrandIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddColumn<List<int>>(
            name: "ExcludedCategoryIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddColumn<List<int>>(
            name: "ExcludedTagIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddColumn<List<int>>(
            name: "IncludedBrandIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddColumn<List<int>>(
            name: "IncludedCategoryIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.AddColumn<List<int>>(
            name: "IncludedTagIds",
            table: "Coupons",
            type: "integer[]",
            nullable: false,
            defaultValueSql: "'{}'");

        migrationBuilder.CreateTable(
            name: "Tags",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "text", nullable: false),
                Slug = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                BadgeColor = table.Column<string>(type: "text", nullable: true),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Tags", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Campaigns",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "text", nullable: false),
                Slug = table.Column<string>(type: "text", nullable: false),
                BanglaName = table.Column<string>(type: "text", nullable: true),
                Eyebrow = table.Column<string>(type: "text", nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                BannerImageUrl = table.Column<string>(type: "text", nullable: true),
                MobileBannerImageUrl = table.Column<string>(type: "text", nullable: true),
                StartDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                EndDate = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                ShowCountdown = table.Column<bool>(type: "boolean", nullable: false),
                ThemeColor = table.Column<string>(type: "text", nullable: false),
                HighlightCouponCode = table.Column<string>(type: "text", nullable: true),
                TagId = table.Column<int>(type: "integer", nullable: true),
                CuratedProductIds = table.Column<List<int>>(type: "integer[]", nullable: false, defaultValueSql: "'{}'"),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                MetaTitle = table.Column<string>(type: "text", nullable: true),
                MetaDescription = table.Column<string>(type: "text", nullable: true),
                OgImageUrl = table.Column<string>(type: "text", nullable: true),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Campaigns", x => x.Id);
                table.ForeignKey(
                    name: "FK_Campaigns_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id");
            });

        migrationBuilder.CreateTable(
            name: "ProductTags",
            columns: table => new
            {
                ProductId = table.Column<int>(type: "integer", nullable: false),
                TagId = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductTags", x => new { x.ProductId, x.TagId });
                table.ForeignKey(
                    name: "FK_ProductTags_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_ProductTags_Tags_TagId",
                    column: x => x.TagId,
                    principalTable: "Tags",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_IsActive_SortOrder",
            table: "Campaigns",
            columns: new[] { "IsActive", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_Slug",
            table: "Campaigns",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_TagId",
            table: "Campaigns",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_ProductTags_TagId",
            table: "ProductTags",
            column: "TagId");

        migrationBuilder.CreateIndex(
            name: "IX_Tags_Slug",
            table: "Tags",
            column: "Slug",
            unique: true,
            filter: "NOT \"IsDeleted\"");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Campaigns");

        migrationBuilder.DropTable(
            name: "ProductTags");

        migrationBuilder.DropTable(
            name: "Tags");

        migrationBuilder.DropColumn(
            name: "ExcludedBrandIds",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "ExcludedCategoryIds",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "ExcludedTagIds",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "IncludedBrandIds",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "IncludedCategoryIds",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "IncludedTagIds",
            table: "Coupons");
    }
}
