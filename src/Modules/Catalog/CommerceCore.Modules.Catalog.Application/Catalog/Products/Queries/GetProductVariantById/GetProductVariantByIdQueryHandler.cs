using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductVariantById;

public sealed class GetProductVariantByIdQueryHandler(
    ICommerceCoreDbContext dbContext)
    : IQueryHandler<
        GetProductVariantByIdQuery,
        GetProductVariantByIdResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<GetProductVariantByIdResult?> Handle(
        GetProductVariantByIdQuery query,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(query.ProductId);
        ProductVariantId variantId = ProductVariantId.From(
            query.ProductVariantId);

        Product? product = await _dbContext.Products
            .AsNoTracking()
            .Include(item => item.Variants)
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        ProductVariant? variant = product.Variants.SingleOrDefault(
            item => item.Id == variantId);

        if (variant is null)
            return null;

        return new GetProductVariantByIdResult(
            product.Id.Value,
            variant.Id.Value,
            variant.Sku.Value,
            variant.Price.Amount,
            variant.Price.Currency,
            variant.Status.ToString(),
            variant.IsDefault,
            variant.Options);
    }
}