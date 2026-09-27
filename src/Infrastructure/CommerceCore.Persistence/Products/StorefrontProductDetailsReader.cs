using System.Text.Json;
using CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.Serialization;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace CommerceCore.Persistence.Products;

public sealed class StorefrontProductDetailsReader(
    CommerceCoreDbContext dbContext,
    ITenantContext tenantContext) : IStorefrontProductDetailsReader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters =
        {
            new AttributeValueBagJsonConverter()
        }
    };

    public async Task<StorefrontProductDetails?> ReadAsync(
        GetStorefrontProductQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(
            query.VariantPageSize,
            1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            query.VariantPageSize,
            GetStorefrontProductQuery.MaximumVariantPageSize);

        if (query.AfterVariantId == Guid.Empty)
        {
            throw new ArgumentException(
                "The variant cursor cannot be empty.",
                nameof(query));
        }

        cancellationToken.ThrowIfCancellationRequested();

        if (!tenantContext.IsResolved ||
            tenantContext.TenantId is not TenantId tenantId ||
            tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A resolved tenant is required to read storefront products.");
        }

        ProductId productId = ProductId.From(query.ProductId);

        List<StorefrontProductDetailsSql.Row> rows = await dbContext.Database
            .SqlQueryRaw<StorefrontProductDetailsSql.Row>(
                StorefrontProductDetailsSql.Query,
                new NpgsqlParameter("productId", NpgsqlDbType.Uuid) { Value = productId.Value },
                new NpgsqlParameter("tenantId", NpgsqlDbType.Uuid) { Value = tenantId.Value },
                new NpgsqlParameter("afterVariantId", NpgsqlDbType.Uuid) { Value = (object?)query.AfterVariantId ?? DBNull.Value },
                new NpgsqlParameter("take", NpgsqlDbType.Integer) { Value = query.VariantPageSize + 1 })
            .ToListAsync(cancellationToken);

        if (rows.Count == 0)
        {
            return null;
        }

        StorefrontProductDetailsSql.Row product = rows[0];

        StorefrontVariantDetails[] variants = rows
            .Where(row => row.VariantId.HasValue)
            .Take(query.VariantPageSize)
            .Select(MapVariant)
            .ToArray();

        Guid? nextAfterVariantId = rows.Count > query.VariantPageSize
            ? variants[^1].ProductVariantId
            : null;

        return new StorefrontProductDetails(
            product.ProductId,
            product.ProductTypeId,
            ReadLocalizedName(product.NameJson, tenantContext.DefaultLocale),
            product.BasePriceAmount,
            product.Currency,
            variants,
            nextAfterVariantId);
    }

    private static StorefrontVariantDetails MapVariant(
        StorefrontProductDetailsSql.Row row)
    {
        if (row.VariantId is not Guid variantId ||
            row.VariantSku is not string sku ||
            row.VariantPriceAmount is not decimal priceAmount ||
            row.VariantCurrency is not string currency ||
            row.IsDefault is not bool isDefault ||
            row.OptionsJson is not string optionsJson)
        {
            throw new InvalidOperationException(
                "Stored storefront variant data is incomplete.");
        }

        AttributeValueBag options = JsonSerializer.Deserialize<AttributeValueBag>(
            optionsJson,
            JsonOptions) ?? throw new InvalidOperationException(
                "Stored variant options cannot be null.");

        return new StorefrontVariantDetails(
            variantId,
            sku,
            priceAmount,
            currency,
            isDefault,
            MapOptions(options));
    }

    private static string ReadLocalizedName(
        string json,
        string? locale)
    {
        LocalizedText name = LocalizedTextJsonSerializer.Deserialize(json);

        LanguageCode language = locale is not null
            ? LanguageCode.Create(locale)
            : name.DefaultLanguage;

        return name.GetOrDefault(language);
    }

    private static Dictionary<string, string> MapOptions(
        AttributeValueBag options)
    {
        return options.Values.ToDictionary(
            pair => pair.Key.Value,
            pair => pair.Value is AttributeValue.SingleSelect selected
                ? selected.OptionCode
                : throw new InvalidOperationException(
                    "Stored variant options must contain SingleSelect values."),
            StringComparer.Ordinal);
    }
}
