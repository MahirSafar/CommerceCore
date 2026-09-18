using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforcePlatformLifecycleValues : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_tenants_status",
                schema: "platform",
                table: "tenants",
                sql: "status IN ('Active', 'Inactive')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_tenant_memberships_status",
                schema: "platform",
                table: "tenant_memberships",
                sql: "status IN ('Active', 'Inactive')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_tenants_status",
                schema: "platform",
                table: "tenants");

            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_tenant_memberships_status",
                schema: "platform",
                table: "tenant_memberships");
        }
    }
}
