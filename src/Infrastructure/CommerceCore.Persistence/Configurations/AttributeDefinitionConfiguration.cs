using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CommerceCore.Persistence.Configurations;

public sealed class AttributeDefinitionConfiguration : IEntityTypeConfiguration<AttributeDefinition>
{
    private static readonly ValueConverter<MeasurementUnitFamily?, string?>
        NullableMeasurementUnitFamilyConverter = new(
            family => family.HasValue ? family.Value.Value : null,
            value => value == null
                ? null
                : MeasurementUnitFamily.Create(value));

    public void Configure(EntityTypeBuilder<AttributeDefinition> builder)
    {
        builder.ToTable("attribute_definitions", schema: "catalog");

        builder.HasKey(definition => definition.Id);

        builder.Property(definition => definition.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => AttributeDefinitionId.From(value))
            .ValueGeneratedNever();

        builder.Property(definition => definition.TenantId)
            .HasColumnName("tenant_id")
            .HasConversion(
                id => id.Value,
                value => TenantId.From(value))
            .IsRequired();

        builder.HasAlternateKey(definition => new { definition.TenantId, definition.Id })
            .HasName("ux_attribute_definitions_tenant_id_id");

        builder.Property(definition => definition.ProductTypeId)
            .HasColumnName("product_type_id")
            .HasConversion(
                id => id.Value,
                value => ProductTypeId.From(value))
            .IsRequired();

        builder.Property(definition => definition.Key)
            .HasColumnName("key")
            .HasMaxLength(AttributeKey.MaximumLength)
            .HasConversion(
                key => key.Value,
                value => AttributeKey.Create(value))
            .IsRequired();

        builder.Property(definition => definition.DataType)
            .HasColumnName("data_type")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(definition => definition.Scope)
            .HasColumnName("scope")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(definition => definition.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();

        builder.Property(definition => definition.EnforcementStatus)
            .HasColumnName("enforcement_status")
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(definition => definition.IsDeprecated)
            .HasColumnName("is_deprecated")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(definition => definition.DisplayOrder)
            .HasColumnName("display_order")
            .IsRequired();

        builder.Property(definition => definition.MinimumValue)
            .HasColumnName("minimum_value")
            .HasPrecision(18, 4);

        builder.Property(definition => definition.MaximumValue)
            .HasColumnName("maximum_value")
            .HasPrecision(18, 4);

        builder.Property(definition => definition.MinimumLength)
            .HasColumnName("minimum_length");

        builder.Property(definition => definition.MaximumLength)
            .HasColumnName("maximum_length");

        builder.Property(definition => definition.MeasurementUnitFamily)
            .HasColumnName("measurement_unit_family")
            .HasMaxLength(MeasurementUnitFamily.MaximumLength)
            .HasConversion(NullableMeasurementUnitFamilyConverter);

        builder.HasOne<ProductType>()
            .WithMany(productType => productType.AttributeDefinitions)
            .HasPrincipalKey(productType => new { productType.TenantId, productType.Id })
            .HasForeignKey(definition => new { definition.TenantId, definition.ProductTypeId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attribute_definitions_product_type");

        builder.HasMany(definition => definition.Options)
            .WithOne()
            .HasPrincipalKey(definition => new { definition.TenantId, definition.Id })
            .HasForeignKey(option => new { option.TenantId, option.AttributeDefinitionId })
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attribute_options_attribute_definition");

        builder.Navigation(definition => definition.Options)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(definition => new
        {
            definition.TenantId,
            definition.ProductTypeId,
            definition.Key
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_definitions_tenant_product_type_key");

        builder.HasIndex(definition => new
        {
            definition.TenantId,
            definition.ProductTypeId,
            definition.DisplayOrder
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_definitions_tenant_product_type_display_order");
    }
}