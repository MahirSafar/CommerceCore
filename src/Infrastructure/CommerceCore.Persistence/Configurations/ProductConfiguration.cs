using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Text.Json;

namespace CommerceCore.Persistence.Configurations;

public sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private sealed record LocalizedTextDocument(string DefaultLanguage, Dictionary<string, string> Translations);
    private static string SerializeLocalizedText(LocalizedText localizedText) => JsonSerializer.Serialize(new LocalizedTextDocument(localizedText.DefaultLanguage.Value, localizedText.Translations.ToDictionary(pair => pair.Key.Value, pair => pair.Value, StringComparer.Ordinal)));
    private static LocalizedText DeserializeLocalizedText(string json)
    {
        var document = JsonSerializer.Deserialize<LocalizedTextDocument>(json, JsonOptions) ?? throw new InvalidOperationException("LocalizedText JSON cannot be null.");

        return LocalizedText.Create(LanguageCode.Create(document.DefaultLanguage), document.Translations.Select(pair => new KeyValuePair<LanguageCode, string>(LanguageCode.Create(pair.Key), pair.Value)));
    }
    private static readonly ValueConverter<LocalizedText, string> LocalizedTextConverter = new(localizedText => SerializeLocalizedText(localizedText), json => DeserializeLocalizedText(json));

    private static readonly ValueComparer<LocalizedText> LocalizedTextComparer = new((left,right) => ReferenceEquals(left, right) || left.Equals(right), value => value.GetHashCode(), value => value);

    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("products", schema: "catalog");

        builder.HasKey(product => product.Id);

        builder.HasQueryFilter(product => !product.IsDeleted);

        builder.Property(product => product.Id)
            .HasColumnName("id")
            .HasConversion(
                productId => productId.Value,
                value => ProductId.From(value))
            .ValueGeneratedNever();

        builder.Property(product => product.Status)
            .HasColumnName("status")
            .HasConversion<string>()
            .HasMaxLength(16)
            .IsRequired();

        builder.Property(product => product.Name)
            .HasColumnName("name")
            .HasColumnType("jsonb")
            .HasConversion(LocalizedTextConverter, LocalizedTextComparer)
            .IsRequired();

        builder.Property(product => product.IsDeleted)
            .HasColumnName("is_deleted")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(product => product.DeletedAtUtc)
            .HasColumnName("deleted_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(product => product.DeletedBy)
            .HasColumnName("deleted_by")
            .HasMaxLength(200);

        builder.Property(product => product.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(product => product.CreatedBy)
            .HasColumnName("created_by")
            .HasMaxLength(200);

        builder.Property(product => product.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(product => product.UpdatedBy)
            .HasColumnName("updated_by")
            .HasMaxLength(200);

        builder.Property<uint>("xmin")
            .IsRowVersion();

        builder.Ignore(product => product.DomainEvents);

        builder.HasIndex(product => new
        {
            product.Status,
            product.IsDeleted
        });

        builder.OwnsOne(product => product.Price, price =>
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
            }).HasDatabaseName("ix_products_price_currency_amount");
        });

        builder.Navigation(product => product.Price).IsRequired();

    }
}
