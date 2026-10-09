using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "catalog",
                table: "skus",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "catalog",
                table: "products",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "catalog",
                table: "product_images",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "catalog",
                table: "product_images");
        }
    }
}
