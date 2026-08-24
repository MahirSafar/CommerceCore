using System.Text.Json;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Persistence.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommerceCore.Persistence.Configurations;

public sealed class ProductVariantConfiguration
    : IEntityTypeConfiguration<ProductVariant>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web)
        {
            Converters =
            {
                new AttributeValueBagJsonConverter()
            }
        };

    private static readonly ValueComparer<AttributeValueBag>
        AttributeValueBagComparer = new(
            (left, right) => left!.Equals(right),
            value => value.GetHashCode(),
            value => JsonSerializer.Deserialize<AttributeValueBag>(
                JsonSerializer.Serialize(value, JsonOptions),
                JsonOptions) ?? AttributeValueBag.Empty);

    public void Configure(EntityTypeBuilder<ProductVariant> builder)
    {
        builder.ToTable("product_variants", schema: "catalog");

        builder.HasKey(variant => variant.Id);

        builder.Property(variant => variant.Id)
            .HasColumnName("id")
            .HasConversion(
                id => id.Value,
                value => ProductVariantId.From(value))
            .ValueGeneratedNever();

        builder.Property<ProductId>("ProductId")
            .HasColumnName("product_id")
            .HasConversion(
                id => id.Value,
                value => ProductId.From(value))
            .IsRequired();

        builder.Property(variant => variant.Sku)
            .HasColumnName("sku")
            .HasMaxLength(VariantSku.MaximumLength)
            .HasConversion(
                sku => sku.Value,
                value => VariantSku.Create(value))
            .IsRequired();

        builder.Property(variant => variant.Options)
            .HasColumnName("options")
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'::jsonb")
            .HasConversion(
                options => JsonSerializer.Serialize(options, JsonOptions),
                json => JsonSerializer.Deserialize<AttributeValueBag>(
                    json,
                    JsonOptions) ?? AttributeValueBag.Empty,
                AttributeValueBagComparer)
            .IsRequired();

        builder.Property(variant => variant.IsDefault)
            .HasColumnName("is_default")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(variant => variant.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.OwnsOne(variant => variant.Price, price =>
        {
            price.Property(money => money.Amount)
                .HasColumnName("price_amount")
                .HasPrecision(18, 4)
                .IsRequired();

            price.Property(money => money.Currency)
                .HasColumnName("price_currency")
                .HasMaxLength(3)
                .IsRequired();

            price.HasIndex(money => new
            {
                money.Currency,
                money.Amount
            }).HasDatabaseName(
                "ix_product_variants_price_currency_amount");
        });

        builder.Navigation(variant => variant.Price)
            .IsRequired();

        builder.HasIndex(variant => variant.Sku)
            .IsUnique()
            .HasDatabaseName("ux_product_variants_sku");

        builder.HasIndex("ProductId", nameof(ProductVariant.Options))
            .IsUnique()
            .HasDatabaseName(
                "ux_product_variants_product_id_options");

        builder.HasIndex("ProductId")
            .IsUnique()
            .HasFilter("\"is_default\" = TRUE")
            .HasDatabaseName(
                "ux_product_variants_default_per_product");
    }
}