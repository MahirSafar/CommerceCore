using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceLowercaseStorefrontHostNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE platform.storefronts
                SET host_name = lower(host_name)
                WHERE host_name <> lower(host_name);
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_platform_storefronts_host_name_lowercase",
                schema: "platform",
                table: "storefronts",
                sql: "host_name = lower(host_name)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_platform_storefronts_host_name_lowercase",
                schema: "platform",
                table: "storefronts");
        }
    }
}
