using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommerceCore.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddOutboxDeliveryState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_tenant_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_on_utc",
                schema: "outbox",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "lease_expires_on_utc",
                schema: "outbox",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lease_id",
                schema: "outbox",
                table: "messages",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "next_attempt_on_utc",
                schema: "outbox",
                table: "messages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_dispatch",
                schema: "outbox",
                table: "messages",
                columns: new[] { "tenant_id", "next_attempt_on_utc", "occurred_on_utc", "id" },
                filter: "\"processed_on_utc\" IS NULL AND \"dead_lettered_on_utc\" IS NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_messages_attempt_count",
                schema: "outbox",
                table: "messages",
                sql: "attempt_count >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_messages_lease_pair",
                schema: "outbox",
                table: "messages",
                sql: "(lease_id IS NULL) = (lease_expires_on_utc IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_outbox_messages_terminal_state",
                schema: "outbox",
                table: "messages",
                sql: "NOT (\r\n    processed_on_utc IS NOT NULL\r\n    AND dead_lettered_on_utc IS NOT NULL\r\n)\r\nAND (\r\n    (processed_on_utc IS NULL AND dead_lettered_on_utc IS NULL)\r\n    OR (lease_id IS NULL AND next_attempt_on_utc IS NULL)\r\n)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_outbox_messages_tenant_dispatch",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_messages_attempt_count",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_messages_lease_pair",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropCheckConstraint(
                name: "ck_outbox_messages_terminal_state",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "dead_lettered_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "lease_expires_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "lease_id",
                schema: "outbox",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "next_attempt_on_utc",
                schema: "outbox",
                table: "messages");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_messages_tenant_pending_occurred_on_utc",
                schema: "outbox",
                table: "messages",
                columns: new[] { "tenant_id", "occurred_on_utc" },
                filter: "\"processed_on_utc\" IS NULL");
        }
    }
}
