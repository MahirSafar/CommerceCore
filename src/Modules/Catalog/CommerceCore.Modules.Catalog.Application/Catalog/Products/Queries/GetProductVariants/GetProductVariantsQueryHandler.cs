using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductVariants;

public sealed class GetProductVariantsQueryHandler(
    ICommerceCoreDbContext dbContext)
    : IQueryHandler<
        GetProductVariantsQuery,
        GetProductVariantsResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<GetProductVariantsResult?> Handle(
        GetProductVariantsQuery query,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(query.ProductId);

        Product? product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.Variants)
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        GetProductVariantListItem[] variants = product.Variants
            .OrderByDescending(item => item.IsDefault)
            .ThenBy(item => item.Sku.Value, StringComparer.Ordinal)
            .Select(item => new GetProductVariantListItem(
                item.Id.Value,
                item.Sku.Value,
                item.Price.Amount,
                item.Price.Currency,
                item.Status.ToString(),
                item.IsDefault,
                item.Options))
            .ToArray();

        return new GetProductVariantsResult(
            product.Id.Value,
            variants);
    }
}