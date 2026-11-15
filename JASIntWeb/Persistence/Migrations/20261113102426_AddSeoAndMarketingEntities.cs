using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261113102426_AddSeoAndMarketingEntities : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BanglaDescription",
            table: "Products",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BanglaName",
            table: "Products",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OgImageUrl",
            table: "Products",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Synonyms",
            table: "Products",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsVerifiedPurchase",
            table: "ProductReviews",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "ImageAltText",
            table: "ProductImages",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdId",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "AdSetId",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CampaignId",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FirstTouchSource",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LandingPath",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "LastTouchSource",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaEventId",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "MetaPurchaseSent",
            table: "Orders",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "CatalogFeedToken",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "FacebookDomainVerification",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "GoogleSearchConsoleVerification",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MessengerPageId",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaPurchaseValueMode",
            table: "DeliverySettings",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "WhatsAppNumber",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BottomSeoCopy",
            table: "Categories",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "IntroText",
            table: "Categories",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaDescription",
            table: "Categories",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaTitle",
            table: "Categories",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OgImageUrl",
            table: "Categories",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerEmail",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerName",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "CustomerPhone",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbc",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbclid",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbp",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTimeOffset>(
            name: "LastActivityAt",
            table: "Carts",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmCampaign",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmContent",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmMedium",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmSource",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmTerm",
            table: "Carts",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BottomSeoCopy",
            table: "Brands",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "IntroText",
            table: "Brands",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaDescription",
            table: "Brands",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaTitle",
            table: "Brands",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "OgImageUrl",
            table: "Brands",
            type: "text",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Guides",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Title = table.Column<string>(type: "text", nullable: false),
                Slug = table.Column<string>(type: "text", nullable: false),
                Summary = table.Column<string>(type: "text", nullable: false),
                ContentHtml = table.Column<string>(type: "text", nullable: false),
                Author = table.Column<string>(type: "text", nullable: false),
                FeaturedImageUrl = table.Column<string>(type: "text", nullable: true),
                MetaTitle = table.Column<string>(type: "text", nullable: true),
                MetaDescription = table.Column<string>(type: "text", nullable: true),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Guides", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "UrlRedirects",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                OldPath = table.Column<string>(type: "text", nullable: false),
                NewPath = table.Column<string>(type: "text", nullable: false),
                StatusCode = table.Column<int>(type: "integer", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UrlRedirects", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Orders_MetaPurchaseSent",
            table: "Orders",
            column: "MetaPurchaseSent");

        migrationBuilder.CreateIndex(
            name: "IX_Orders_UtmCampaign",
            table: "Orders",
            column: "UtmCampaign");

        migrationBuilder.CreateIndex(
            name: "IX_Carts_CustomerPhone",
            table: "Carts",
            column: "CustomerPhone");

        migrationBuilder.CreateIndex(
            name: "IX_Carts_Status",
            table: "Carts",
            column: "Status");

        migrationBuilder.CreateIndex(
            name: "IX_Guides_IsPublished_PublishedAt",
            table: "Guides",
            columns: new[] { "IsPublished", "PublishedAt" });

        migrationBuilder.CreateIndex(
            name: "IX_Guides_Slug",
            table: "Guides",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_UrlRedirects_OldPath",
            table: "UrlRedirects",
            column: "OldPath",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "Guides");

        migrationBuilder.DropTable(
            name: "UrlRedirects");

        migrationBuilder.DropIndex(
            name: "IX_Orders_MetaPurchaseSent",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Orders_UtmCampaign",
            table: "Orders");

        migrationBuilder.DropIndex(
            name: "IX_Carts_CustomerPhone",
            table: "Carts");

        migrationBuilder.DropIndex(
            name: "IX_Carts_Status",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "BanglaDescription",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "BanglaName",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "OgImageUrl",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Synonyms",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "IsVerifiedPurchase",
            table: "ProductReviews");

        migrationBuilder.DropColumn(
            name: "ImageAltText",
            table: "ProductImages");

        migrationBuilder.DropColumn(
            name: "AdId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "AdSetId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "CampaignId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "FirstTouchSource",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "LandingPath",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "LastTouchSource",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "MetaEventId",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "MetaPurchaseSent",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "CatalogFeedToken",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "FacebookDomainVerification",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "GoogleSearchConsoleVerification",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "MessengerPageId",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "MetaPurchaseValueMode",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "WhatsAppNumber",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "BottomSeoCopy",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "IntroText",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "MetaDescription",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "MetaTitle",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "OgImageUrl",
            table: "Categories");

        migrationBuilder.DropColumn(
            name: "CustomerEmail",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "CustomerName",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "CustomerPhone",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "Fbc",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "Fbclid",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "Fbp",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "LastActivityAt",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "UtmCampaign",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "UtmContent",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "UtmMedium",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "UtmSource",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "UtmTerm",
            table: "Carts");

        migrationBuilder.DropColumn(
            name: "BottomSeoCopy",
            table: "Brands");

        migrationBuilder.DropColumn(
            name: "IntroText",
            table: "Brands");

        migrationBuilder.DropColumn(
            name: "MetaDescription",
            table: "Brands");

        migrationBuilder.DropColumn(
            name: "MetaTitle",
            table: "Brands");

        migrationBuilder.DropColumn(
            name: "OgImageUrl",
            table: "Brands");
    }
}
