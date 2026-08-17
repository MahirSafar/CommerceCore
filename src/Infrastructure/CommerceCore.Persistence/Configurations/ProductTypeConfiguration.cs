using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace CommerceCore.Persistence.Configurations;

public sealed class ProductTypeConfiguration : IEntityTypeConfiguration<ProductType>
{
    private static readonly ValueConverter<ProductTypeId?, Guid?>
        NullableProductTypeIdConverter = new(
            id => id.HasValue ? id.Value.Value : null,
            value => value.HasValue ? ProductTypeId.From(value.Value) : null);

    public void Configure(EntityTypeBuilder<ProductType> builder)
    {
        builder.ToTable("product_types", schema: "catalog");

        builder.HasKey(productType => productType.Id);

        builder.Property(productType => productType.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => ProductTypeId.From(value))
            .ValueGeneratedNever();

        builder.Property(productType => productType.Code)
            .HasColumnName("code")
            .HasMaxLength(ProductTypeCode.MaximumLength)
            .HasConversion(
                code => code.Value,
                value => ProductTypeCode.Create(value))
            .IsRequired();

        builder.Property(productType => productType.ParentProductTypeId)
            .HasColumnName("parent_product_type_id")
            .HasConversion(NullableProductTypeIdConverter);

        builder.Property<LTree>("path")
            .HasColumnName("path")
            .HasColumnType("ltree")
            .ValueGeneratedOnAdd()
            .IsRequired();

        builder.Property(productType => productType.IsAssignable)
            .HasColumnName("is_assignable")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(productType => productType.SchemaVersion)
            .HasColumnName("schema_version")
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(productType => productType.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(productType => productType.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(productType => productType.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(productType => productType.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.Ignore(productType => productType.DomainEvents);

        builder.HasOne<ProductType>()
            .WithMany()
            .HasForeignKey(productType => productType.ParentProductTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_product_types_parent_product_type");

        builder.HasMany(productType => productType.AttributeDefinitions)
            .WithOne()
            .HasForeignKey(definition => definition.ProductTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_attribute_definitions_product_type");

        builder.Navigation(productType => productType.AttributeDefinitions)
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(productType => productType.Code)
            .IsUnique()
            .HasDatabaseName("ux_product_types_code");

        builder.HasIndex("path")
            .HasMethod("gist")
            .HasDatabaseName("ix_product_types_path_gist");

        builder.HasIndex(productType => productType.ParentProductTypeId)
            .HasDatabaseName("ix_product_types_parent_product_type_id");
    }
}