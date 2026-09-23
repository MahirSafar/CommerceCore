using CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence.Products;

public sealed class StorefrontProductDetailsReader(
    CommerceCoreDbContext dbContext,
    ITenantContext tenantContext) : IStorefrontProductDetailsReader
{
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

        IQueryable<ProductVariant> candidateVariants = dbContext.Set<ProductVariant>();

        if (query.AfterVariantId is Guid afterVariantId)
        {
            // Compare UUIDs in PostgreSQL using the same ordering as ORDER BY.
            // FromSql parameterizes the cursor.
            candidateVariants = dbContext.Set<ProductVariant>()
                .FromSql(
                    $"""
                    SELECT *
                    FROM catalog.product_variants
                    WHERE id > {afterVariantId}
                    """);
        }

        var row = await dbContext.Products
            .AsNoTracking()
            .AsSingleQuery()
            .Where(product =>
                product.Id == productId &&
                product.TenantId == tenantId &&
                product.Status == ProductStatus.Active &&
                !product.IsDeleted)
            .Select(product => new
            {
                product.Id,
                product.ProductTypeId,
                product.Name,
                BasePriceAmount = product.Price.Amount,
                product.Price.Currency,
                Variants = candidateVariants
                    .Where(variant =>
                        variant.TenantId == tenantId &&
                        EF.Property<ProductId>(variant, "ProductId") == product.Id &&
                        variant.Status == ProductVariantStatus.Active)
                    .OrderBy(variant => variant.Id)
                    .Select(variant => new
                    {
                        variant.Id,
                        variant.Sku,
                        BasePriceAmount = variant.Price.Amount,
                        variant.Price.Currency,
                        variant.IsDefault,
                        variant.Options
                    })
                    .Take(query.VariantPageSize + 1)
                    .ToArray()
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (row is null)
        {
            return null;
        }

        LanguageCode language = tenantContext.DefaultLocale is string locale
            ? LanguageCode.Create(locale)
            : row.Name.DefaultLanguage;

        StorefrontVariantDetails[] variants = row.Variants
            .Take(query.VariantPageSize)
            .Select(variant => new StorefrontVariantDetails(
                variant.Id.Value,
                variant.Sku.Value,
                variant.BasePriceAmount,
                variant.Currency,
                variant.IsDefault,
                MapOptions(variant.Options)))
            .ToArray();

        Guid? nextAfterVariantId = row.Variants.Length > query.VariantPageSize
            ? variants[^1].ProductVariantId
            : null;

        return new StorefrontProductDetails(
            row.Id.Value,
            row.ProductTypeId.Value,
            row.Name.GetOrDefault(language),
            row.BasePriceAmount,
            row.Currency,
            variants,
            nextAfterVariantId);
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
