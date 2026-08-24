using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProductVariant;

public sealed class DeactivateProductVariantCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<
        DeactivateProductVariantCommand,
        DeactivateProductVariantResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<DeactivateProductVariantResult?> Handle(
        DeactivateProductVariantCommand command,
        CancellationToken cancellationToken)
    {
        Product? product = await _dbContext.Products
            .Include(item => item.Variants)
            .SingleOrDefaultAsync(
                item => item.Id.Value == command.ProductId,
                cancellationToken);

        if (product is null)
            return null;

        ProductVariant? variant = product.Variants.SingleOrDefault(
            item => item.Id.Value == command.ProductVariantId);

        if (variant is null)
            return null;

        bool deactivated = product.DeactivateVariant(variant.Id);

        if (deactivated)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeactivateProductVariantResult(
            product.Id.Value,
            variant.Id.Value,
            variant.Status.ToString(),
            deactivated);
    }
}