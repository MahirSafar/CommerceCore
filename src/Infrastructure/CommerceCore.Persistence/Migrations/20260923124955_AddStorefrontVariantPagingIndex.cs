using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddStorefrontVariantPagingIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_product_variants_tenant_product_active_id",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "product_id", "id" },
                filter: "\"status\" = 'Active'");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_product_variants_tenant_product_active_id",
                schema: "catalog",
                table: "product_variants");
        }
    }
}
