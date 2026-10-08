using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Kamus.Catalog.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SoftDelete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_skus_code",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropIndex(
                name: "ix_skus_product_id_color_size",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropIndex(
                name: "ix_products_slug",
                schema: "catalog",
                table: "products");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "catalog",
                table: "skus",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_id",
                schema: "catalog",
                table: "skus",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_name",
                schema: "catalog",
                table: "skus",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "catalog",
                table: "products",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_name",
                schema: "catalog",
                table: "products",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "deleted_at",
                schema: "catalog",
                table: "product_images",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "deleted_by_id",
                schema: "catalog",
                table: "product_images",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "deleted_by_name",
                schema: "catalog",
                table: "product_images",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_code",
                schema: "catalog",
                table: "skus",
                column: "code",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_skus_deleted_at",
                schema: "catalog",
                table: "skus",
                column: "deleted_at",
                filter: "deleted_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_skus_product_id_color_size",
                schema: "catalog",
                table: "skus",
                columns: new[] { "product_id", "color", "size" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_products_deleted_at",
                schema: "catalog",
                table: "products",
                column: "deleted_at",
                filter: "deleted_at IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_products_slug",
                schema: "catalog",
                table: "products",
                column: "slug",
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_product_images_deleted_at",
                schema: "catalog",
                table: "product_images",
                column: "deleted_at",
                filter: "deleted_at IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_skus_code",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropIndex(
                name: "ix_skus_deleted_at",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropIndex(
                name: "ix_skus_product_id_color_size",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropIndex(
                name: "ix_products_deleted_at",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_slug",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_product_images_deleted_at",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropColumn(
                name: "deleted_by_id",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropColumn(
                name: "deleted_by_name",
                schema: "catalog",
                table: "skus");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "deleted_by_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "deleted_by_name",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "deleted_at",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "deleted_by_id",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.DropColumn(
                name: "deleted_by_name",
                schema: "catalog",
                table: "product_images");

            migrationBuilder.CreateIndex(
                name: "ix_skus_code",
                schema: "catalog",
                table: "skus",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_skus_product_id_color_size",
                schema: "catalog",
                table: "skus",
                columns: new[] { "product_id", "color", "size" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_products_slug",
                schema: "catalog",
                table: "products",
                column: "slug",
                unique: true);
        }
    }
}
