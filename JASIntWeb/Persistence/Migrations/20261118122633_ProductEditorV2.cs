using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261118122633_ProductEditorV2 : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Do not change historical identifiers to resolve collisions implicitly.
        migrationBuilder.Sql("""
            DO $$ BEGIN
              IF EXISTS (SELECT 1 FROM "ProductVariants" GROUP BY upper(btrim("SKU")) HAVING COUNT(*) > 1) THEN
                RAISE EXCEPTION 'SKU normalization collision: resolve duplicate upper(trim(SKU)) values explicitly, including archived variants, before applying ProductEditorV2.';
              END IF;
            END $$;
            """);
        migrationBuilder.AddColumn<bool>(
            name: "IsRetired",
            table: "ProductAttributeOptions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "MetadataJson",
            table: "ProductAttributeOptions",
            type: "jsonb",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<bool>(
            name: "IsRetired",
            table: "ProductAttributeDefinitions",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<string>(
            name: "MetadataSchemaJson",
            table: "ProductAttributeDefinitions",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "SwatchFieldKey",
            table: "ProductAttributeDefinitions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<uint>(
            name: "xmin",
            table: "InventoryItems",
            type: "xid",
            rowVersion: true,
            nullable: false,
            defaultValue: 0u);

        migrationBuilder.AddColumn<string>(
            name: "MetadataJson",
            table: "AttributeOptions",
            type: "jsonb",
            nullable: false,
            defaultValue: "{}");

        migrationBuilder.AddColumn<string>(
            name: "MetadataSchemaJson",
            table: "AttributeDefinitions",
            type: "jsonb",
            nullable: false,
            defaultValue: "[]");

        migrationBuilder.AddColumn<string>(
            name: "SwatchFieldKey",
            table: "AttributeDefinitions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "NormalizedSku",
            table: "ProductVariants",
            type: "text",
            nullable: false,
            computedColumnSql: "upper(btrim(\"SKU\"))",
            stored: true);

        migrationBuilder.CreateTable(
            name: "ProductEditorReceipts",
            columns: table => new
            {
                ActorId = table.Column<int>(type: "integer", nullable: false),
                OperationId = table.Column<Guid>(type: "uuid", nullable: false),
                ProductId = table.Column<int>(type: "integer", nullable: false),
                CommandKind = table.Column<string>(type: "text", nullable: false),
                PayloadHash = table.Column<string>(type: "text", nullable: false),
                ResultJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ProductEditorReceipts", x => new { x.ActorId, x.OperationId });
            });

        migrationBuilder.CreateIndex(
            name: "UX_ProductVariants_NormalizedSku",
            table: "ProductVariants",
            column: "NormalizedSku",
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "ProductEditorReceipts");

        migrationBuilder.DropIndex(
            name: "UX_ProductVariants_NormalizedSku",
            table: "ProductVariants");

        migrationBuilder.DropColumn(
            name: "NormalizedSku",
            table: "ProductVariants");

        migrationBuilder.DropColumn(
            name: "IsRetired",
            table: "ProductAttributeOptions");

        migrationBuilder.DropColumn(
            name: "MetadataJson",
            table: "ProductAttributeOptions");

        migrationBuilder.DropColumn(
            name: "IsRetired",
            table: "ProductAttributeDefinitions");

        migrationBuilder.DropColumn(
            name: "MetadataSchemaJson",
            table: "ProductAttributeDefinitions");

        migrationBuilder.DropColumn(
            name: "SwatchFieldKey",
            table: "ProductAttributeDefinitions");

        migrationBuilder.DropColumn(
            name: "xmin",
            table: "InventoryItems");

        migrationBuilder.DropColumn(
            name: "MetadataJson",
            table: "AttributeOptions");

        migrationBuilder.DropColumn(
            name: "MetadataSchemaJson",
            table: "AttributeDefinitions");

        migrationBuilder.DropColumn(
            name: "SwatchFieldKey",
            table: "AttributeDefinitions");
    }
}
