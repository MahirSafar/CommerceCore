using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class StorefrontConfiguration : IEntityTypeConfiguration<Storefront>
{
    public void Configure(EntityTypeBuilder<Storefront> builder)
    {
        builder.ToTable("storefronts", schema: "platform");

        builder.HasKey(storefront => storefront.Id);

        builder.Property(storefront => storefront.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(storefront => storefront.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(storefront => storefront.HostName)
            .HasColumnName("host_name")
            .HasMaxLength(255)
            .IsRequired();

        builder.HasIndex(storefront => storefront.HostName)
            .IsUnique()
            .HasDatabaseName("ix_platform_storefronts_host_name");

        builder.Property(storefront => storefront.MarketCode)
            .HasColumnName("market_code")
            .HasMaxLength(10)
            .IsRequired();

        builder.Property(storefront => storefront.DefaultLocale)
            .HasColumnName("default_locale")
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(storefront => storefront.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(storefront => storefront.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_storefronts_tenant");
    }
}
