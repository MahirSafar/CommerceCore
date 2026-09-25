using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceAttributeDefinitionTenantForeignKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attribute_definitions_product_type",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropIndex(
                name: "IX_attribute_definitions_product_type_id",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.AddForeignKey(
                name: "fk_attribute_definitions_product_type",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "product_type_id" },
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attribute_definitions_product_type",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_product_type_id",
                schema: "catalog",
                table: "attribute_definitions",
                column: "product_type_id");

            migrationBuilder.AddForeignKey(
                name: "fk_attribute_definitions_product_type",
                schema: "catalog",
                table: "attribute_definitions",
                column: "product_type_id",
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
