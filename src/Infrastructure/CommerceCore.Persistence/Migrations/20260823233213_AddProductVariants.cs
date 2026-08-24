using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    sku = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    options = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    is_default = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_variants", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_variants_product",
                        column: x => x.product_id,
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_variants_price_currency_amount",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "price_currency", "price_amount" });

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_default_per_product",
                schema: "catalog",
                table: "product_variants",
                column: "product_id",
                unique: true,
                filter: "\"is_default\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_product_id_options",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "product_id", "options" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_sku",
                schema: "catalog",
                table: "product_variants",
                column: "sku",
                unique: true);

            migrationBuilder.Sql(
                """
                INSERT INTO catalog.product_variants (
                    id,
                    sku,
                    price_amount,
                    price_currency,
                    options,
                    is_default,
                    status,
                    product_id)
                SELECT
                    p.id,
                    'LEGACY-' || UPPER(REPLACE(p.id::text, '-', '')),
                    p.price_amount,
                    p.price_currency,
                    '{}'::jsonb,
                    TRUE,
                    CASE
                        WHEN p.is_deleted THEN 'Archived'
                        WHEN p.status = 'Active' THEN 'Active'
                        WHEN p.status = 'Inactive' THEN 'Inactive'
                        ELSE 'Draft'
                    END,
                    p.id
                FROM catalog.products AS p;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "catalog");
        }
    }
}
