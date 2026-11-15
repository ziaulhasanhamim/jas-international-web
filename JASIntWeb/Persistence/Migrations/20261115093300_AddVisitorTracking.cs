using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace JASIntWeb.Persistence.Migrations;

/// <inheritdoc />
public partial class _20261115093300_AddVisitorTracking : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VisitorLogs",
            columns: table => new
            {
                Id = table.Column<long>(type: "bigint", nullable: false)
                    .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                VisitorId = table.Column<string>(type: "text", nullable: false),
                SessionId = table.Column<string>(type: "text", nullable: false),
                UserId = table.Column<int>(type: "integer", nullable: true),
                Path = table.Column<string>(type: "text", nullable: false),
                Title = table.Column<string>(type: "text", nullable: true),
                Referrer = table.Column<string>(type: "text", nullable: true),
                ReferrerDomain = table.Column<string>(type: "text", nullable: true),
                IpAddress = table.Column<string>(type: "text", nullable: true),
                City = table.Column<string>(type: "text", nullable: true),
                Country = table.Column<string>(type: "text", nullable: true),
                DeviceType = table.Column<string>(type: "text", nullable: false),
                Browser = table.Column<string>(type: "text", nullable: false),
                OperatingSystem = table.Column<string>(type: "text", nullable: false),
                UserAgent = table.Column<string>(type: "text", nullable: true),
                UtmSource = table.Column<string>(type: "text", nullable: true),
                UtmMedium = table.Column<string>(type: "text", nullable: true),
                UtmCampaign = table.Column<string>(type: "text", nullable: true),
                Fbclid = table.Column<string>(type: "text", nullable: true),
                DurationSeconds = table.Column<int>(type: "integer", nullable: false),
                Timestamp = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                IsBounce = table.Column<bool>(type: "boolean", nullable: false),
                ConvertedOrderId = table.Column<int>(type: "integer", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_VisitorLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_City",
            table: "VisitorLogs",
            column: "City");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_DeviceType",
            table: "VisitorLogs",
            column: "DeviceType");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_Path",
            table: "VisitorLogs",
            column: "Path");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_ReferrerDomain",
            table: "VisitorLogs",
            column: "ReferrerDomain");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_SessionId",
            table: "VisitorLogs",
            column: "SessionId");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_Timestamp",
            table: "VisitorLogs",
            column: "Timestamp");

        migrationBuilder.CreateIndex(
            name: "IX_VisitorLogs_VisitorId",
            table: "VisitorLogs",
            column: "VisitorId");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(
            name: "VisitorLogs");
    }
}
