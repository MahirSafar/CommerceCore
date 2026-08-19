using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.ProductTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class ProductTypeEffectiveSchemaConfiguration : IEntityTypeConfiguration<ProductTypeEffectiveSchema>
{
    public void Configure(EntityTypeBuilder<ProductTypeEffectiveSchema> builder)
    {
        builder.ToTable(
            "product_type_effective_schema",
            schema: "catalog");

        builder.HasKey(schema => schema.ProductTypeId);

        builder.Property(schema => schema.ProductTypeId)
            .HasColumnName("product_type_id")
            .HasConversion(
                id => id.Value,
                value => ProductTypeId.From(value))
            .ValueGeneratedNever();

        builder.Property(schema => schema.EffectiveSchemaVersion)
            .HasColumnName("effective_schema_version")
            .HasDefaultValue(0L)
            .IsRequired();

        builder.Property(schema => schema.Schema)
            .HasColumnName("schema")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .IsRequired();

        builder.Property(schema => schema.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone")
            .HasDefaultValueSql("CURRENT_TIMESTAMP")
            .IsRequired();

        builder.HasOne<ProductType>()
            .WithOne()
            .HasForeignKey<ProductTypeEffectiveSchema>(
                schema => schema.ProductTypeId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName(
                "fk_product_type_effective_schema_product_type");
    }
}