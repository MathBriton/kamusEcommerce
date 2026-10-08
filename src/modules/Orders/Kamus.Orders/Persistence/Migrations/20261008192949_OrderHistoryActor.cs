using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Orders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderHistoryActor : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "actor_kind",
                schema: "orders",
                table: "order_status_history",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "actor_name",
                schema: "orders",
                table: "order_status_history",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "actor_kind",
                schema: "orders",
                table: "order_status_history");

            migrationBuilder.DropColumn(
                name: "actor_name",
                schema: "orders",
                table: "order_status_history");
        }
    }
}
