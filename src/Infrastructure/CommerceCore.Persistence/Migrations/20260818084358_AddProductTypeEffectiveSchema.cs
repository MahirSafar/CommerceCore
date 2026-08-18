using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTypeEffectiveSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_type_effective_schema",
                schema: "catalog",
                columns: table => new
                {
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    schema = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_type_effective_schema", x => x.product_type_id);
                    table.ForeignKey(
                        name: "fk_product_type_effective_schema_product_type",
                        column: x => x.product_type_id,
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_product_type_effective_schema_version_nonnegative",
                schema: "catalog",
                table: "product_type_effective_schema",
                sql: "\"schema_version\" >= 0");

            migrationBuilder.Sql(
                """
                INSERT INTO catalog.product_type_effective_schema (
                    product_type_id,
                    schema_version,
                    schema,
                    updated_at_utc)
                SELECT
                    id,
                    schema_version,
                    jsonb_build_object(
                        'attributes',
                        '[]'::jsonb),
                    CURRENT_TIMESTAMP
                FROM catalog.product_types;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_type_effective_schema",
                schema: "catalog");
        }
    }
}
