using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class AttributeOptionConfiguration : IEntityTypeConfiguration<AttributeOption>
{
    public void Configure(EntityTypeBuilder<AttributeOption> builder)
    {
        builder.ToTable("attribute_options", schema: "catalog");

        builder.HasKey(option => option.Id);

        builder.Property(option => option.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => AttributeOptionId.From(value))
            .ValueGeneratedNever();

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
            option.AttributeDefinitionId,
            option.Code
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_options_definition_code");

        builder.HasIndex(option => new
        {
            option.AttributeDefinitionId,
            option.DisplayOrder
        })
            .IsUnique()
            .HasDatabaseName("ux_attribute_options_definition_display_order");
    }
}