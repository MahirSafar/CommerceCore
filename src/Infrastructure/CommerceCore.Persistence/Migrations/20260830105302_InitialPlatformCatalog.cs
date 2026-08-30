using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialPlatformCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "catalog");

            migrationBuilder.EnsureSchema(
                name: "outbox");

            migrationBuilder.EnsureSchema(
                name: "platform");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:ltree", ",,");

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
                name: "messages",
                schema: "outbox",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    type = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    content = table.Column<string>(type: "jsonb", nullable: false),
                    processed_on_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempt_count = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_messages", x => x.id);
                    table.ForeignKey(
                        name: "fk_outbox_messages_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    parent_product_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_assignable = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    own_schema_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
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
                    table.UniqueConstraint("ux_product_types_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_product_types_own_schema_version_nonnegative", "\"own_schema_version\" >= 0");
                    table.ForeignKey(
                        name: "fk_product_types_parent_product_type",
                        columns: x => new { x.tenant_id, x.parent_product_type_id },
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_types_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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

            migrationBuilder.CreateTable(
                name: "attribute_definitions",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.UniqueConstraint("ux_attribute_definitions_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_attribute_definitions_display_order_nonnegative", "\"display_order\" >= 0");
                    table.CheckConstraint("ck_attribute_definitions_enforcement_status", "\"enforcement_status\" IN ('Draft', 'Backfilling', 'Enforced')\nAND (\n    \"is_required\"\n    OR \"enforcement_status\" = 'Enforced'\n)");
                    table.CheckConstraint("ck_attribute_definitions_integer_range", "\"data_type\" <> 'Integer'\nOR (\n    (\"minimum_value\" IS NULL OR trunc(\"minimum_value\") = \"minimum_value\")\n    AND (\"maximum_value\" IS NULL OR trunc(\"maximum_value\") = \"maximum_value\")\n)");
                    table.CheckConstraint("ck_attribute_definitions_length_range", "(\"minimum_length\" IS NULL OR \"minimum_length\" >= 0)\nAND (\"maximum_length\" IS NULL OR \"maximum_length\" >= 0)\nAND (\n    \"minimum_length\" IS NULL\n    OR \"maximum_length\" IS NULL\n    OR \"minimum_length\" <= \"maximum_length\"\n)");
                    table.CheckConstraint("ck_attribute_definitions_measurement_unit_family", "(\n    \"data_type\" = 'Measurement'\n    AND \"measurement_unit_family\" IS NOT NULL\n)\nOR (\n    \"data_type\" <> 'Measurement'\n    AND \"measurement_unit_family\" IS NULL\n)");
                    table.CheckConstraint("ck_attribute_definitions_numeric_range", "\"minimum_value\" IS NULL\nOR \"maximum_value\" IS NULL\nOR \"minimum_value\" <= \"maximum_value\"");
                    table.CheckConstraint("ck_attribute_definitions_numeric_type", "\"data_type\" IN ('Integer', 'Decimal', 'Measurement')\nOR (\"minimum_value\" IS NULL AND \"maximum_value\" IS NULL)");
                    table.CheckConstraint("ck_attribute_definitions_text_length", "\"data_type\" = 'Text'\nOR (\"minimum_length\" IS NULL AND \"maximum_length\" IS NULL)");
                    table.ForeignKey(
                        name: "fk_attribute_definitions_product_type",
                        column: x => x.product_type_id,
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attribute_definitions_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_type_effective_schema",
                schema: "catalog",
                columns: table => new
                {
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    effective_schema_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    schema = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "CURRENT_TIMESTAMP")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_type_effective_schema", x => new { x.tenant_id, x.product_type_id });
                    table.CheckConstraint("ck_product_type_effective_schema_effective_version_nonnegative", "\"effective_schema_version\" >= 0");
                    table.ForeignKey(
                        name: "fk_product_type_effective_schema_product_type",
                        columns: x => new { x.tenant_id, x.product_type_id },
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_type_effective_schema_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "products",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "jsonb", nullable: false),
                    price_amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    price_currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    product_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    specifications = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'{}'::jsonb"),
                    validated_against_version = table.Column<long>(type: "bigint", nullable: false, defaultValue: 0L),
                    status = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    updated_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    deleted_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_products", x => x.id);
                    table.UniqueConstraint("ux_products_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_products_specifications_is_object", "jsonb_typeof(\"specifications\") = 'object'");
                    table.CheckConstraint("ck_products_specifications_key_count", "catalog.jsonb_key_count(\"specifications\") <= 50");
                    table.ForeignKey(
                        name: "fk_products_product_type",
                        columns: x => new { x.tenant_id, x.product_type_id },
                        principalSchema: "catalog",
                        principalTable: "product_types",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_products_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "attribute_options",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    attribute_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    display_order = table.Column<int>(type: "integer", nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_attribute_options", x => x.id);
                    table.UniqueConstraint("ux_attribute_options_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.CheckConstraint("ck_attribute_options_display_order_nonnegative", "\"display_order\" >= 0");
                    table.ForeignKey(
                        name: "fk_attribute_options_attribute_definition",
                        columns: x => new { x.tenant_id, x.attribute_definition_id },
                        principalSchema: "catalog",
                        principalTable: "attribute_definitions",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_attribute_options_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_variants",
                schema: "catalog",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
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
                    table.UniqueConstraint("ux_product_variants_tenant_id_id", x => new { x.tenant_id, x.id });
                    table.ForeignKey(
                        name: "fk_product_variants_product",
                        columns: x => new { x.tenant_id, x.product_id },
                        principalSchema: "catalog",
                        principalTable: "products",
                        principalColumns: new[] { "tenant_id", "id" },
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_product_variants_tenant",
                        column: x => x.tenant_id,
                        principalSchema: "platform",
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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
                name: "ix_outbox_messages_tenant_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages",
                columns: new[] { "tenant_id", "occurred_on_utc" },
                filter: "\"processed_on_utc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_product_types_path_gist",
                schema: "catalog",
                table: "product_types",
                column: "path")
                .Annotation("Npgsql:IndexMethod", "gist");

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
                name: "ix_product_variants_price_currency_amount",
                schema: "catalog",
                table: "product_variants",
                columns: new[] { "price_currency", "price_amount" });

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
                name: "ix_products_price_currency_amount",
                schema: "catalog",
                table: "products",
                columns: new[] { "price_currency", "price_amount" });

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

            migrationBuilder.Sql(
                """
                CREATE SEQUENCE catalog.schema_revision_seq
                    AS bigint
                    START WITH 1
                    INCREMENT BY 1
                    MINVALUE 1
                    NO MAXVALUE
                    NO CYCLE;
                """);

            migrationBuilder.Sql(
                """
                CREATE FUNCTION catalog.trg_fn_product_type_path()
                RETURNS trigger
                LANGUAGE plpgsql
                AS $$
                DECLARE
                    parent_path ltree;
                BEGIN
                    IF TG_OP = 'UPDATE' THEN
                        IF NEW.code IS DISTINCT FROM OLD.code THEN
                            RAISE EXCEPTION
                                'Product type code is immutable. Create a new ProductType instead.';
                        END IF;

                        IF NEW.parent_product_type_id
                            IS DISTINCT FROM OLD.parent_product_type_id THEN
                            RAISE EXCEPTION
                                'Product type parent is immutable. A subtree move requires a dedicated migration.';
                        END IF;
                    END IF;

                    IF NEW.parent_product_type_id IS NULL THEN
                        NEW.path := NEW.code::ltree;
                        RETURN NEW;
                    END IF;

                    SELECT path
                    INTO parent_path
                    FROM catalog.product_types
                    WHERE tenant_id = NEW.tenant_id
                      AND id = NEW.parent_product_type_id
                    FOR KEY SHARE;

                    IF NOT FOUND THEN
                        RAISE EXCEPTION
                            'Parent ProductType % does not exist.',
                            NEW.parent_product_type_id
                            USING ERRCODE = '23503';
                    END IF;

                    NEW.path := parent_path || NEW.code::ltree;
                    RETURN NEW;
                END;
                $$;
                """);

            migrationBuilder.Sql(
                """
                CREATE TRIGGER trg_product_types_set_path
                BEFORE INSERT OR UPDATE OF code, parent_product_type_id
                ON catalog.product_types
                FOR EACH ROW
                EXECUTE FUNCTION catalog.trg_fn_product_type_path();
                """);

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
                    CREATE POLICY tenant_isolation_policy ON {table}
                        USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
                        WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_product_types_set_path
                ON catalog.product_types;
                """);

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS catalog.trg_fn_product_type_path();
                """);

            migrationBuilder.Sql("""
                DROP SEQUENCE IF EXISTS catalog.schema_revision_seq;
                """);

            migrationBuilder.DropTable(
                name: "attribute_options",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "messages",
                schema: "outbox");

            migrationBuilder.DropTable(
                name: "product_type_effective_schema",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "product_variants",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "storefronts",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "tenant_memberships",
                schema: "platform");

            migrationBuilder.DropTable(
                name: "attribute_definitions",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "products",
                schema: "catalog");

            migrationBuilder.Sql("""
                DROP FUNCTION IF EXISTS catalog.jsonb_key_count(jsonb);
                """);

            migrationBuilder.DropTable(
                name: "product_types",
                schema: "catalog");

            migrationBuilder.DropTable(
                name: "tenants",
                schema: "platform");
        }
    }
}
