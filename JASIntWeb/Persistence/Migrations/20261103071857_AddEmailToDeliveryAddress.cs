using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace JASIntWeb.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddEmailToDeliveryAddress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "DeliveryAddresses",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Email",
                table: "DeliveryAddresses");
        }
    }
}
