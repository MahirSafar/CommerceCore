using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class TenantMembershipConfiguration : IEntityTypeConfiguration<TenantMembership>
{
    public void Configure(EntityTypeBuilder<TenantMembership> builder)
    {
        builder.ToTable("tenant_memberships", schema: "platform");

        builder.HasKey(membership => new { membership.TenantId, membership.UserSubject });

        builder.Property(membership => membership.TenantId)
            .HasColumnName("tenant_id")
            .IsRequired();

        builder.Property(membership => membership.UserSubject)
            .HasColumnName("user_subject")
            .HasMaxLength(255)
            .IsRequired();

        builder.Property(membership => membership.Role)
            .HasColumnName("role")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(membership => membership.Status)
            .HasColumnName("status")
            .HasMaxLength(50)
            .IsRequired();

        builder.HasIndex(membership => membership.UserSubject)
            .HasDatabaseName("ix_platform_tenant_memberships_user_subject");

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(membership => membership.TenantId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_tenant_memberships_tenant");
    }
}
