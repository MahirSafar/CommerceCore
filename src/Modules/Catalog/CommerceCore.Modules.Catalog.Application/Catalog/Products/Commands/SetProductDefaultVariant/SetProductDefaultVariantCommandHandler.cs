using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductDefaultVariant;

public sealed class SetProductDefaultVariantCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<
        SetProductDefaultVariantCommand,
        SetProductDefaultVariantResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<SetProductDefaultVariantResult?> Handle(
        SetProductDefaultVariantCommand command,
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

        bool defaultChanged = product.SetDefaultVariant(variant.Id);

        if (defaultChanged)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetProductDefaultVariantResult(
            product.Id.Value,
            variant.Id.Value,
            defaultChanged);
    }
}