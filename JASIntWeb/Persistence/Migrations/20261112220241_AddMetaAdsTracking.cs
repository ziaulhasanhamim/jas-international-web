using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261112220241_AddMetaAdsTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ClientIpAddress",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "ClientUserAgent",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbc",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbclid",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Fbp",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmCampaign",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmContent",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmMedium",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmSource",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "UtmTerm",
            table: "Orders",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaAccessToken",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaPixelId",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "MetaTestEventCode",
            table: "DeliverySettings",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "MetaTrackingEnabled",
            table: "DeliverySettings",
            type: "boolean",
            nullable: false,
            defaultValue: false);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ClientIpAddress",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "ClientUserAgent",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "Fbc",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "Fbclid",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "Fbp",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "UtmCampaign",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "UtmContent",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "UtmMedium",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "UtmSource",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "UtmTerm",
            table: "Orders");

        migrationBuilder.DropColumn(
            name: "MetaAccessToken",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "MetaPixelId",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "MetaTestEventCode",
            table: "DeliverySettings");

        migrationBuilder.DropColumn(
            name: "MetaTrackingEnabled",
            table: "DeliverySettings");
    }
}
