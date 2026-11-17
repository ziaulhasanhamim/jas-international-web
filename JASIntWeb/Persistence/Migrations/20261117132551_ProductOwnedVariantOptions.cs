using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261117132551_ProductOwnedVariantOptions : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "UX_ProductAttributeOptions_Value",
            table: "ProductAttributeOptions");

        migrationBuilder.AddColumn<decimal>(
            name: "CompareAtPrice",
            table: "ProductVariants",
            type: "numeric",
            nullable: true);

        migrationBuilder.AddColumn<long>(
            name: "CatalogRevision",
            table: "Products",
            type: "bigint",
            nullable: false,
            defaultValue: 0L);

        migrationBuilder.AlterColumn<int>(
            name: "AttributeOptionId",
            table: "ProductAttributeOptions",
            type: "integer",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "integer");

        migrationBuilder.AddColumn<string>(
            name: "Code",
            table: "ProductAttributeOptions",
            type: "character varying(120)",
            maxLength: 120,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "NormalizedValue",
            table: "ProductAttributeOptions",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Value",
            table: "ProductAttributeOptions",
            type: "character varying(200)",
            maxLength: 200,
            nullable: false,
            defaultValue: "");

        migrationBuilder.Sql("""
            UPDATE "ProductAttributeOptions" AS local
            SET "Value" = source."Value", "Code" = source."Code",
                "NormalizedValue" = upper(trim(normalize(source."Value", NFKC)))
            FROM "AttributeOptions" AS source WHERE source."Id" = local."AttributeOptionId";
            UPDATE "ProductVariants" AS variant SET "CompareAtPrice" = product."CompareAtPrice"
            FROM "Products" AS product WHERE product."Id" = variant."ProductId";
            """);

        migrationBuilder.CreateIndex(
            name: "IX_ProductAttributeOptions_ProductAttributeDefinitionId_Code",
            table: "ProductAttributeOptions",
            columns: new[] { "ProductAttributeDefinitionId", "Code" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "UX_ProductAttributeOptions_Value",
            table: "ProductAttributeOptions",
            columns: new[] { "ProductAttributeDefinitionId", "NormalizedValue" },
            unique: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DO $$ BEGIN
                IF EXISTS (SELECT 1 FROM "ProductAttributeOptions" AS local LEFT JOIN "AttributeOptions" AS source
                    ON source."Id" = local."AttributeOptionId"
                    WHERE source."Id" IS NULL OR source."Value" <> local."Value" OR source."Code" <> local."Code") THEN
                    RAISE EXCEPTION 'Product-owned values cannot be downgraded without losing data. Restore a database backup instead.';
                END IF;
            END $$;
            """);
        migrationBuilder.DropIndex(
            name: "IX_ProductAttributeOptions_ProductAttributeDefinitionId_Code",
            table: "ProductAttributeOptions");

        migrationBuilder.DropIndex(
            name: "UX_ProductAttributeOptions_Value",
            table: "ProductAttributeOptions");

        migrationBuilder.DropColumn(
            name: "CompareAtPrice",
            table: "ProductVariants");

        migrationBuilder.DropColumn(
            name: "CatalogRevision",
            table: "Products");

        migrationBuilder.DropColumn(
            name: "Code",
            table: "ProductAttributeOptions");

        migrationBuilder.DropColumn(
            name: "NormalizedValue",
            table: "ProductAttributeOptions");

        migrationBuilder.DropColumn(
            name: "Value",
            table: "ProductAttributeOptions");

        migrationBuilder.AlterColumn<int>(
            name: "AttributeOptionId",
            table: "ProductAttributeOptions",
            type: "integer",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "integer",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "UX_ProductAttributeOptions_Value",
            table: "ProductAttributeOptions",
            columns: new[] { "ProductAttributeDefinitionId", "AttributeOptionId" },
            unique: true);
    }
}
