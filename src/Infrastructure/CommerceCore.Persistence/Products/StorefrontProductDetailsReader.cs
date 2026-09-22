using CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;
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

        if (!tenantContext.IsResolved ||
            tenantContext.TenantId is not TenantId tenantId ||
            tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A resolved tenant is required to read storefront products.");
        }

        ProductId productId = ProductId.From(query.ProductId);

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
                Variants = product.Variants
                    .Where(variant =>
                        variant.TenantId == tenantId &&
                        variant.Status == ProductVariantStatus.Active)
                    .OrderByDescending(variant => variant.IsDefault)
                    .ThenBy(variant => variant.Id)
                    .Select(variant => new
                    {
                        variant.Id,
                        variant.Sku,
                        BasePriceAmount = variant.Price.Amount,
                        variant.Price.Currency,
                        variant.IsDefault
                    })
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
            .Select(variant => new StorefrontVariantDetails(
                variant.Id.Value,
                variant.Sku.Value,
                variant.BasePriceAmount,
                variant.Currency,
                variant.IsDefault))
            .ToArray();

        return new StorefrontProductDetails(
            row.Id.Value,
            row.ProductTypeId.Value,
            row.Name.GetOrDefault(language),
            row.BasePriceAmount,
            row.Currency,
            variants);
    }
}
