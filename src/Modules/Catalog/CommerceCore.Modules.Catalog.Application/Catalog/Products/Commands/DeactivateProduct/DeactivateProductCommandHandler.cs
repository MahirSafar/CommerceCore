using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProduct;

public sealed class DeactivateProductCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<DeactivateProductCommand, DeactivateProductResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<DeactivateProductResult?> Handle(
        DeactivateProductCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        bool changed = product.Deactivate();

        if (changed)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new DeactivateProductResult(
            product.Id.Value,
            product.Status.ToString());
    }
}