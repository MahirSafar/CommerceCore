using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants", schema: "platform");

        builder.HasKey(tenant => tenant.Id);

        builder.Property(tenant => tenant.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => TenantId.From(value))
            .ValueGeneratedNever();

        builder.Property(tenant => tenant.Slug)
            .HasColumnName("slug")
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(tenant => tenant.Slug)
            .IsUnique()
            .HasDatabaseName("ix_platform_tenants_slug");

        builder.Property(tenant => tenant.Name)
            .HasColumnName("name")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(tenant => tenant.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(tenant => tenant.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();
    }
}
