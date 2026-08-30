using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
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
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .Include(item => item.Variants)
            .SingleOrDefaultAsync(
                item => item.Id == productId,
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