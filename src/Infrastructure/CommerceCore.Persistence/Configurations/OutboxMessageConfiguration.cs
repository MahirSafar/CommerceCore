using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("messages", schema: "outbox");

        builder.HasKey(message => message.Id);

        builder.Property(message => message.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(message => message.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                id => id.Value,
                value => TenantId.From(value))
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_outbox_messages_tenant");

        builder.Property(message => message.OccurredOnUtc)
            .HasColumnName("occurred_on_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(message => message.Type)
            .HasColumnName("type")
            .HasMaxLength(500)
            .IsRequired();

        builder.Property(message => message.Content)
            .HasColumnName("content")
            .HasColumnType("jsonb")
            .IsRequired();

        builder.Property(message => message.ProcessedOnUtc)
            .HasColumnName("processed_on_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(message => message.AttemptCount)
            .HasColumnName("attempt_count")
            .HasDefaultValue(0)
            .IsRequired();

        builder.Property(message => message.LastError)
            .HasColumnName("last_error")
            .HasColumnType("text");

        builder.HasIndex(message => new { message.TenantId, message.OccurredOnUtc })
            .HasDatabaseName("ix_outbox_messages_tenant_pending_occurred_on_utc")
            .HasFilter("\"processed_on_utc\" IS NULL");
    }
}
