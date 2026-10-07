using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Orders.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrackingCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "tracking_code",
                schema: "orders",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_orders_created_at",
                schema: "orders",
                table: "orders",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_orders_status_created_at",
                schema: "orders",
                table: "orders",
                columns: new[] { "status", "created_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_orders_created_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropIndex(
                name: "ix_orders_status_created_at",
                schema: "orders",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "tracking_code",
                schema: "orders",
                table: "orders");
        }
    }
}
