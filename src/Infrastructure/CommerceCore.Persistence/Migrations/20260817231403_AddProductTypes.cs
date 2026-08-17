using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProductTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

            migrationBuilder.CreateTable(
                name: "product_types",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parent_product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_assignable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    schema_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    path = table.Column<string>(type: "ltree", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_types", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_types_parent_product_type",
                        column: x => x.parent_product_type_id,
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_definitions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    data_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    scope = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
                    enforcement_status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    minimum_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    maximum_value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    minimum_length = table.Column<int>(type: "integer", nullable: true),
                    maximum_length = table.Column<int>(type: "integer", nullable: true),
                    measurement_unit_family = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_attribute_definitions_product_type",
                        column: x => x.product_type_id,
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_options",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_options", x => x.id);
                    table.ForeignKey(
                        name: "fk_attribute_options_attribute_definition",
                        column: x => x.attribute_definition_id,
                        principalSchema: "catalog",
                        principalTable: "attribute_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ux_attribute_definitions_product_type_display_order",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "product_type_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attribute_definitions_product_type_key",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "product_type_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attribute_options_definition_code",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "attribute_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attribute_options_definition_display_order",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "attribute_definition_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_types_parent_product_type_id",
                schema: "catalog",
                table: "product_types",
                column: "parent_product_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_path_gist",
                schema: "catalog",
                table: "product_types",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

            migrationBuilder.CreateIndex(
                name: "ux_product_types_code",
                schema: "catalog",
                table: "product_types",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "attribute_options",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "attribute_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_types",
                schema: "catalog");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:ltree", ",,");
        }
    }
}
