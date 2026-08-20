using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTypeSpecifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "product_type_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "specifications",
                schema: "catalog",
                table: "products",
                type: "jsonb",
                nullable: false,
                defaultValueSql: "'{}'::jsonb");

            migrationBuilder.AddColumn<long>(
                name: "validated_against_version",
                schema: "catalog",
                table: "products",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.Sql(
                """
        INSERT INTO catalog.product_types (
            id,
            code,
            is_assignable,
            own_schema_version,
            created_at_utc,
            created_by)
        VALUES (
            '018f20f0-0000-7000-8000-000000000001',
            'legacy_unclassified',
            FALSE,
            0,
            CURRENT_TIMESTAMP,
            'system-migration');
        """);

            migrationBuilder.Sql(
                """
        INSERT INTO catalog.product_type_effective_schema (
            product_type_id,
            effective_schema_version,
            schema,
            updated_at_utc)
        VALUES (
            '018f20f0-0000-7000-8000-000000000001',
            nextval('catalog.schema_revision_seq'),
            jsonb_build_object('attributes', '[]'::jsonb),
            CURRENT_TIMESTAMP);
        """);

            migrationBuilder.Sql(
                """
        UPDATE catalog.products
        SET product_type_id = '018f20f0-0000-7000-8000-000000000001'
        WHERE product_type_id IS NULL;
        """);

            migrationBuilder.AlterColumn<Guid>(
                name: "product_type_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.Sql(
                """
        CREATE FUNCTION catalog.jsonb_key_count(data jsonb)
        RETURNS integer
        LANGUAGE sql
        IMMUTABLE
        PARALLEL SAFE
        AS $$
            SELECT count(*)::integer
            FROM jsonb_object_keys(data);
        $$;
        """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_specifications_is_object",
                schema: "catalog",
                table: "products",
                sql: "jsonb_typeof(\"specifications\") = 'object'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_products_specifications_key_count",
                schema: "catalog",
                table: "products",
                sql: "catalog.jsonb_key_count(\"specifications\") <= 50");

            migrationBuilder.CreateIndex(
                name: "ix_products_not_deleted_product_type_id",
                schema: "catalog",
                table: "products",
                column: "product_type_id",
                filter: "\"is_deleted\" = FALSE");

            migrationBuilder.AddForeignKey(
                name: "fk_products_product_type",
                schema: "catalog",
                table: "products",
                column: "product_type_id",
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_products_product_type",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_not_deleted_product_type_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_specifications_is_object",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropCheckConstraint(
                name: "ck_products_specifications_key_count",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "product_type_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "specifications",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "validated_against_version",
                schema: "catalog",
                table: "products");

            migrationBuilder.Sql(
                """
        DROP FUNCTION catalog.jsonb_key_count(jsonb);
        """);
        }
    }
}
