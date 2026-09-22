using CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence.Products;

public sealed class StorefrontProductReader(
    CommerceCoreDbContext dbContext,
    ITenantContext tenantContext) : IStorefrontProductReader
{
    public async Task<StorefrontProductPage> ReadAsync(
        ListStorefrontProductsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.PageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            query.PageSize,
            ListStorefrontProductsQuery.MaximumPageSize);

        cancellationToken.ThrowIfCancellationRequested();

        if (!tenantContext.IsResolved ||
            tenantContext.TenantId is not TenantId tenantId ||
            tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A resolved tenant is required to read storefront products.");
        }

        IQueryable<Product> products = dbContext.Products;

        if (query.AfterProductId is Guid afterProductId)
        {
            // Keep UUID comparison in PostgreSQL, using the same ordering
            // as the ORDER BY below. The cursor is passed as a SQL parameter.
            products = dbContext.Products.FromSql(
                $"""
                SELECT * FROM catalog.products WHERE id > {afterProductId}
                """);
        }

        products = products
            .AsNoTracking()
            .Where(product =>
                product.TenantId == tenantId &&
                product.Status == ProductStatus.Active &&
                !product.IsDeleted);

        if (query.ProductTypeId is Guid productTypeId)
        {
            ProductTypeId typeId = ProductTypeId.From(productTypeId);
            products = products.Where(
                product => product.ProductTypeId == typeId);
        }

        ProductRow[] rows = await products
            .OrderBy(product => product.Id)
            .Select(product => new ProductRow(
                product.Id,
                product.ProductTypeId,
                product.Name,
                product.Price.Amount,
                product.Price.Currency))
            .Take(query.PageSize + 1)
            .ToArrayAsync(cancellationToken);

        LanguageCode? requestedLanguage = tenantContext.DefaultLocale is string locale
            ? LanguageCode.Create(locale)
            : null;

        StorefrontProductListItem[] items = rows
            .Take(query.PageSize)
            .Select(row => new StorefrontProductListItem(
                row.Id.Value,
                row.ProductTypeId.Value,
                row.Name.GetOrDefault(
                    requestedLanguage ?? row.Name.DefaultLanguage),
                row.BasePriceAmount,
                row.Currency))
            .ToArray();

        Guid? nextAfterProductId = rows.Length > query.PageSize
            ? items[^1].ProductId
            : null;

        return new StorefrontProductPage(items, nextAfterProductId);
    }

    private sealed record ProductRow(
        ProductId Id,
        ProductTypeId ProductTypeId,
        LocalizedText Name,
        decimal BasePriceAmount,
        string Currency);
}
