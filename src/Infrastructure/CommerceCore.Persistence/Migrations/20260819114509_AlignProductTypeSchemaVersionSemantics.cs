using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AlignProductTypeSchemaVersionSemantics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_product_types_schema_version_nonnegative",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_type_effective_schema_version_nonnegative",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.RenameColumn(
                name: "schema_version",
                schema: "catalog",
                table: "product_types",
                newName: "own_schema_version");

            migrationBuilder.RenameColumn(
                name: "schema_version",
                schema: "catalog",
                table: "product_type_effective_schema",
                newName: "effective_schema_version");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_types_own_schema_version_nonnegative",
                schema: "catalog",
                table: "product_types",
                sql: "\"own_schema_version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_type_effective_schema_effective_version_nonnegative",
                schema: "catalog",
                table: "product_type_effective_schema",
                sql: "\"effective_schema_version\" >= 0");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_product_types_own_schema_version_nonnegative",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropCheckConstraint(
                name: "ck_product_type_effective_schema_effective_version_nonnegative",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.RenameColumn(
                name: "own_schema_version",
                schema: "catalog",
                table: "product_types",
                newName: "schema_version");

            migrationBuilder.RenameColumn(
                name: "effective_schema_version",
                schema: "catalog",
                table: "product_type_effective_schema",
                newName: "schema_version");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_types_schema_version_nonnegative",
                schema: "catalog",
                table: "product_types",
                sql: "\"schema_version\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_type_effective_schema_version_nonnegative",
                schema: "catalog",
                table: "product_type_effective_schema",
                sql: "\"schema_version\" >= 0");
        }
    }
}
