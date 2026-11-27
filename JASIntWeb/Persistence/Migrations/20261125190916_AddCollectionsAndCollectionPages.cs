using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261125190916_AddCollectionsAndCollectionPages : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<int>(
            name: "CollectionId",
            table: "HomepageSections",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CollectionId",
            table: "Coupons",
            type: "integer",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "CollectionId",
            table: "Campaigns",
            type: "integer",
            nullable: true);

        migrationBuilder.CreateTable(
            name: "Collections",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                Name = table.Column<string>(type: "text", nullable: false),
                Slug = table.Column<string>(type: "text", nullable: false),
                InternalNotes = table.Column<string>(type: "text", nullable: true),
                Type = table.Column<int>(type: "integer", nullable: false),
                SortMode = table.Column<int>(type: "integer", nullable: false),
                RulesJson = table.Column<string>(type: "jsonb", nullable: true),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                CreatedByUserId = table.Column<int>(type: "integer", nullable: true),
                UpdatedByUserId = table.Column<int>(type: "integer", nullable: true),
                IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                DeletedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Collections", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "CollectionPages",
            columns: table => new
            {
                Id = table.Column<int>(type: "integer", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                CollectionId = table.Column<int>(type: "integer", nullable: false),
                Title = table.Column<string>(type: "text", nullable: false),
                Slug = table.Column<string>(type: "text", nullable: false),
                Eyebrow = table.Column<string>(type: "text", nullable: true),
                Description = table.Column<string>(type: "text", nullable: true),
                BannerImageUrl = table.Column<string>(type: "text", nullable: true),
                MobileBannerImageUrl = table.Column<string>(type: "text", nullable: true),
                ThemeColor = table.Column<string>(type: "text", nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                ShowFilters = table.Column<bool>(type: "boolean", nullable: false),
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
                table.PrimaryKey("PK_CollectionPages", x => x.Id);
                table.ForeignKey(
                    name: "FK_CollectionPages_Collections_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateTable(
            name: "CollectionProducts",
            columns: table => new
            {
                CollectionId = table.Column<int>(type: "integer", nullable: false),
                ProductId = table.Column<int>(type: "integer", nullable: false),
                SortOrder = table.Column<int>(type: "integer", nullable: false),
                AddedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_CollectionProducts", x => new { x.CollectionId, x.ProductId });
                table.ForeignKey(
                    name: "FK_CollectionProducts_Collections_CollectionId",
                    column: x => x.CollectionId,
                    principalTable: "Collections",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_CollectionProducts_Products_ProductId",
                    column: x => x.ProductId,
                    principalTable: "Products",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_HomepageSections_CollectionId",
            table: "HomepageSections",
            column: "CollectionId");

        migrationBuilder.CreateIndex(
            name: "IX_Coupons_CollectionId",
            table: "Coupons",
            column: "CollectionId");

        migrationBuilder.CreateIndex(
            name: "IX_Campaigns_CollectionId",
            table: "Campaigns",
            column: "CollectionId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionPages_CollectionId",
            table: "CollectionPages",
            column: "CollectionId");

        migrationBuilder.CreateIndex(
            name: "IX_CollectionPages_IsPublished_CollectionId",
            table: "CollectionPages",
            columns: new[] { "IsPublished", "CollectionId" });

        migrationBuilder.CreateIndex(
            name: "IX_CollectionPages_Slug",
            table: "CollectionPages",
            column: "Slug",
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_CollectionProducts_CollectionId_SortOrder",
            table: "CollectionProducts",
            columns: new[] { "CollectionId", "SortOrder" });

        migrationBuilder.CreateIndex(
            name: "IX_CollectionProducts_ProductId",
            table: "CollectionProducts",
            column: "ProductId");

        migrationBuilder.CreateIndex(
            name: "IX_Collections_IsDeleted_IsActive_Type",
            table: "Collections",
            columns: new[] { "IsDeleted", "IsActive", "Type" });

        migrationBuilder.CreateIndex(
            name: "IX_Collections_Slug",
            table: "Collections",
            column: "Slug",
            unique: true,
            filter: "NOT \"IsDeleted\"");

        migrationBuilder.AddForeignKey(
            name: "FK_Campaigns_Collections_CollectionId",
            table: "Campaigns",
            column: "CollectionId",
            principalTable: "Collections",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Coupons_Collections_CollectionId",
            table: "Coupons",
            column: "CollectionId",
            principalTable: "Collections",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_HomepageSections_Collections_CollectionId",
            table: "HomepageSections",
            column: "CollectionId",
            principalTable: "Collections",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(
            name: "FK_Campaigns_Collections_CollectionId",
            table: "Campaigns");

        migrationBuilder.DropForeignKey(
            name: "FK_Coupons_Collections_CollectionId",
            table: "Coupons");

        migrationBuilder.DropForeignKey(
            name: "FK_HomepageSections_Collections_CollectionId",
            table: "HomepageSections");

        migrationBuilder.DropTable(
            name: "CollectionPages");

        migrationBuilder.DropTable(
            name: "CollectionProducts");

        migrationBuilder.DropTable(
            name: "Collections");

        migrationBuilder.DropIndex(
            name: "IX_HomepageSections_CollectionId",
            table: "HomepageSections");

        migrationBuilder.DropIndex(
            name: "IX_Coupons_CollectionId",
            table: "Coupons");

        migrationBuilder.DropIndex(
            name: "IX_Campaigns_CollectionId",
            table: "Campaigns");

        migrationBuilder.DropColumn(
            name: "CollectionId",
            table: "HomepageSections");

        migrationBuilder.DropColumn(
            name: "CollectionId",
            table: "Coupons");

        migrationBuilder.DropColumn(
            name: "CollectionId",
            table: "Campaigns");
    }
}
