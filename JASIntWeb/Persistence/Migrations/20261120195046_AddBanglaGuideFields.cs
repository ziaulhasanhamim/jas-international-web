using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261120195046_AddBanglaGuideFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BanglaContentHtml",
            table: "Guides",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BanglaSummary",
            table: "Guides",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "BanglaTitle",
            table: "Guides",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BanglaContentHtml",
            table: "Guides");

        migrationBuilder.DropColumn(
            name: "BanglaSummary",
            table: "Guides");

        migrationBuilder.DropColumn(
            name: "BanglaTitle",
            table: "Guides");
    }
}
