using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCourierAndNotesToOrder : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNotes",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourierName",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourierTrackingId",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CourierTrackingUrl",
                table: "Orders",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AdminNotes",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CourierName",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CourierTrackingId",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "CourierTrackingUrl",
                table: "Orders");
        }
    }
}
