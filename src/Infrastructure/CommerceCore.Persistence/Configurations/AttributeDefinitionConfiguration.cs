using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
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
        builder.ToTable(
            "attribute_definitions",
            "catalog",
            table =>
            {
                table.HasCheckConstraint(
                    "ck_attribute_definitions_display_order_nonnegative",
                    "\"display_order\" >= 0");

                table.HasCheckConstraint(
                    "ck_attribute_definitions_numeric_range",
                    """
                    "minimum_value" IS NULL
                    OR "maximum_value" IS NULL
                    OR "minimum_value" <= "maximum_value"
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_numeric_type",
                    """
                    "data_type" IN ('Integer', 'Decimal', 'Measurement')
                    OR ("minimum_value" IS NULL AND "maximum_value" IS NULL)
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_integer_range",
                    """
                    "data_type" <> 'Integer'
                    OR (
                        ("minimum_value" IS NULL OR trunc("minimum_value") = "minimum_value")
                        AND ("maximum_value" IS NULL OR trunc("maximum_value") = "maximum_value")
                    )
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_text_length",
                    """
                    "data_type" = 'Text'
                    OR ("minimum_length" IS NULL AND "maximum_length" IS NULL)
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_length_range",
                    """
                    ("minimum_length" IS NULL OR "minimum_length" >= 0)
                    AND ("maximum_length" IS NULL OR "maximum_length" >= 0)
                    AND (
                        "minimum_length" IS NULL
                        OR "maximum_length" IS NULL
                        OR "minimum_length" <= "maximum_length"
                    )
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_measurement_unit_family",
                    """
                    (
                        "data_type" = 'Measurement'
                        AND "measurement_unit_family" IS NOT NULL
                    )
                    OR (
                        "data_type" <> 'Measurement'
                        AND "measurement_unit_family" IS NULL
                    )
                    """);

                table.HasCheckConstraint(
                    "ck_attribute_definitions_enforcement_status",
                    """
                    "enforcement_status" IN ('Draft', 'Backfilling', 'Enforced')
                    AND (
                        "is_required"
                        OR "enforcement_status" = 'Enforced'
                    )
                    """);
            });

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

        builder.HasOne<Tenant>()
            .WithMany()
            .HasForeignKey(entity => entity.TenantId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attribute_definitions_tenant");

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