using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiTenancyAndRowLevelSecurity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attribute_options_attribute_definition",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropForeignKey(
                name: "fk_product_type_effective_schema_product_type",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.DropForeignKey(
                name: "fk_product_variants_product",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "fk_products_product_type",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_not_deleted_product_type_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "IX_products_status_is_deleted",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_default_per_product",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_product_id_options",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_sku",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_types_code",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_type_effective_schema",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.DropIndex(
                name: "ix_messages_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropIndex(
                name: "ux_attribute_options_definition_code",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropIndex(
                name: "ux_attribute_options_definition_display_order",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropIndex(
                name: "ux_attribute_definitions_product_type_display_order",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropIndex(
                name: "ux_attribute_definitions_product_type_key",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.RenameIndex(
                name: "ix_product_types_parent_product_type_id",
                schema: "catalog",
                table: "product_types",
                newName: "IX_product_types_parent_product_type_id");

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "products",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "product_variants",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "product_types",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "product_type_effective_schema",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "outbox",
                table: "messages",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "attribute_options",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<Guid>(
                name: "tenant_id",
                schema: "catalog",
                table: "attribute_definitions",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddUniqueConstraint(
                name: "ux_products_tenant_id_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ux_product_variants_tenant_id_id",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ux_product_types_tenant_id_id",
                schema: "catalog",
                table: "product_types",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_type_effective_schema",
                schema: "catalog",
                table: "product_type_effective_schema",
                columns: new[] { "tenant_id", "product_type_id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ux_attribute_options_tenant_id_id",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.AddUniqueConstraint(
                name: "ux_attribute_definitions_tenant_id_id",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "id" });

            migrationBuilder.CreateTable(
                name: "tenants",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    slug = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "storefronts",
                schema: "platform",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    host_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    market_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    default_locale = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_storefronts", x => x.id);
                    table.ForeignKey(
                        name: "fk_storefronts_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                schema: "platform",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    role = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenant_memberships", x => new { x.tenant_id, x.user_subject });
                    table.ForeignKey(
                        name: "fk_tenant_memberships_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_not_deleted_product_type_id",
                schema: "catalog",
                table: "products",
                columns: new[] { "tenant_id", "product_type_id" },
                filter: "\"is_deleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_status_is_deleted",
                schema: "catalog",
                table: "products",
                columns: new[] { "tenant_id", "status", "is_deleted" });

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_tenant_default_per_product",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "product_id" },
                unique: true,
                filter: "\"is_default\" = TRUE");

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_tenant_product_id_options",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "product_id", "options" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_product_variants_tenant_sku",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "sku" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_product_types_tenant_parent_product_type_id",
                schema: "catalog",
                table: "product_types",
                columns: new[] { "tenant_id", "parent_product_type_id" });

            migrationBuilder.CreateIndex(
                name: "ux_product_types_tenant_code",
                schema: "catalog",
                table: "product_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages",
                columns: new[] { "tenant_id", "occurred_on_utc" },
                filter: "\"processed_on_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ux_attribute_options_tenant_definition_code",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "tenant_id", "attribute_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attribute_options_tenant_definition_display_order",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "tenant_id", "attribute_definition_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_attribute_definitions_product_type_id",
                schema: "catalog",
                table: "attribute_definitions",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "ux_attribute_definitions_tenant_product_type_display_order",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "product_type_id", "display_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_attribute_definitions_tenant_product_type_key",
                schema: "catalog",
                table: "attribute_definitions",
                columns: new[] { "tenant_id", "product_type_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_platform_storefronts_host_name",
                schema: "platform",
                table: "storefronts",
                column: "host_name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_storefronts_tenant_id",
                schema: "platform",
                table: "storefronts",
                column: "tenant_id");

            migrationBuilder.CreateIndex(
                name: "ix_platform_tenant_memberships_user_subject",
                schema: "platform",
                table: "tenant_memberships",
                column: "user_subject");

            migrationBuilder.CreateIndex(
                name: "ix_platform_tenants_slug",
                schema: "platform",
                table: "tenants",
                column: "slug",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_attribute_options_attribute_definition",
                schema: "catalog",
                table: "attribute_options",
                columns: new[] { "tenant_id", "attribute_definition_id" },
                principalSchema: "catalog",
                principalTable: "attribute_definitions",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_product_type_effective_schema_product_type",
                schema: "catalog",
                table: "product_type_effective_schema",
                columns: new[] { "tenant_id", "product_type_id" },
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_product_variants_product",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "tenant_id", "product_id" },
                principalSchema: "catalog",
                principalTable: "products",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_products_product_type",
                schema: "catalog",
                table: "products",
                columns: new[] { "tenant_id", "product_type_id" },
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumns: new[] { "tenant_id", "id" },
                onDelete: ReferentialAction.Restrict);

            // Enable and Force Row Level Security (RLS) on all tenant-owned tables
            var rlsTables = new[]
            {
                "catalog.products",
                "catalog.product_variants",
                "catalog.product_types",
                "catalog.attribute_definitions",
                "catalog.attribute_options",
                "catalog.product_type_effective_schema",
                "outbox.messages"
            };

            foreach (var table in rlsTables)
            {
                migrationBuilder.Sql($"""
                    ALTER TABLE {table} ENABLE ROW LEVEL SECURITY;
                    ALTER TABLE {table} FORCE ROW LEVEL SECURITY;
                    DROP POLICY IF EXISTS tenant_isolation_policy ON {table};
                    CREATE POLICY tenant_isolation_policy ON {table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_attribute_options_attribute_definition",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropForeignKey(
                name: "fk_product_type_effective_schema_product_type",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.DropForeignKey(
                name: "fk_product_variants_product",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropForeignKey(
                name: "fk_products_product_type",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropTable(
                name: "storefronts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "platform");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_products_tenant_id_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_tenant_not_deleted_product_type_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropIndex(
                name: "ix_products_tenant_status_is_deleted",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_product_variants_tenant_id_id",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_tenant_default_per_product",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_tenant_product_id_options",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropIndex(
                name: "ux_product_variants_tenant_sku",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_product_types_tenant_id_id",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropIndex(
                name: "ix_product_types_tenant_parent_product_type_id",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropIndex(
                name: "ux_product_types_tenant_code",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropPrimaryKey(
                name: "PK_product_type_effective_schema",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_tenant_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_attribute_options_tenant_id_id",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropIndex(
                name: "ux_attribute_options_tenant_definition_code",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropIndex(
                name: "ux_attribute_options_tenant_definition_display_order",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropUniqueConstraint(
                name: "ux_attribute_definitions_tenant_id_id",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropIndex(
                name: "IX_attribute_definitions_product_type_id",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropIndex(
                name: "ux_attribute_definitions_tenant_product_type_display_order",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropIndex(
                name: "ux_attribute_definitions_tenant_product_type_key",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "product_variants");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "product_types");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "product_type_effective_schema");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "attribute_options");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                schema: "catalog",
                table: "attribute_definitions");

            migrationBuilder.RenameIndex(
                name: "IX_product_types_parent_product_type_id",
                schema: "catalog",
                table: "product_types",
                newName: "ix_product_types_parent_product_type_id");

            migrationBuilder.AddPrimaryKey(
                name: "PK_product_type_effective_schema",
                schema: "catalog",
                table: "product_type_effective_schema",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_not_deleted_product_type_id",
                schema: "catalog",
                table: "products",
                column: "product_type_id",
                filter: "\"is_deleted\" = FALSE");

            migrationBuilder.CreateIndex(
                name: "IX_products_status_is_deleted",
                schema: "catalog",
                table: "products",
                columns: new[] { "status", "is_deleted" });

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

            migrationBuilder.CreateIndex(
                name: "ux_product_types_code",
                schema: "catalog",
                table: "product_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_messages_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages",
                column: "occurred_on_utc",
                filter: "\"processed_on_utc\" IS NULL");

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

            migrationBuilder.AddForeignKey(
                name: "fk_attribute_options_attribute_definition",
                schema: "catalog",
                table: "attribute_options",
                column: "attribute_definition_id",
                principalSchema: "catalog",
                principalTable: "attribute_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_product_type_effective_schema_product_type",
                schema: "catalog",
                table: "product_type_effective_schema",
                column: "product_type_id",
                principalSchema: "catalog",
                principalTable: "product_types",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_product_variants_product",
                schema: "catalog",
                table: "product_variants",
                column: "product_id",
                principalSchema: "catalog",
                principalTable: "products",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

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
    }
}
