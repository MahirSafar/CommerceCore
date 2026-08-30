using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class AttributeOptionConfiguration : IEntityTypeConfiguration<AttributeOption>
{
    public void Configure(EntityTypeBuilder<AttributeOption> builder)
    {
        builder.ToTable(
            "attribute_options",
            "catalog",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_attribute_options_display_order_nonnegative",
                    "\"display_order\" >= 0");
            });

        builder.HasKey(option => option.Id);

        builder.Property(option => option.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => AttributeOptionId.From(value))
            .ValueGeneratedNever();

        builder.Property(option => option.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                id => id.Value,
                value => TenantId.From(value))
            .IsRequired();

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attribute_options_tenant");

        builder.HasAlternateKey(option => new { option.TenantId, option.Id })
            .HasName("ux_attribute_options_tenant_id_id");

        builder.Property(option => option.AttributeDefinitionId)
            .HasColumnName("attribute_definition_id")
            .HasConversion(
                id => id.Value,
                value => AttributeDefinitionId.From(value))
            .IsRequired();

        builder.Property(option => option.Code)
            .HasColumnName("code")
            .HasMaxLength(AttributeOptionCode.MaximumLength)
            .HasConversion(
                code => code.Value,
                value => AttributeOptionCode.Create(value))
            .IsRequired();

        builder.Property(option => option.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(option => option.IsDeprecated)
            .HasColumnName("is_deprecated")
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(option => new
        {
            option.TenantId,
            option.AttributeDefinitionId,
            option.Code
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_options_tenant_definition_code");

        builder.HasIndex(option => new
        {
            option.TenantId,
            option.AttributeDefinitionId,
            option.DisplayOrder
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_options_tenant_definition_display_order");
    }
}