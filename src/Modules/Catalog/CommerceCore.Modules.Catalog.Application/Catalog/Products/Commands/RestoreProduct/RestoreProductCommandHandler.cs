using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.RestoreProduct;

public sealed class RestoreProductCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<RestoreProductCommand, RestoreProductResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<RestoreProductResult?> Handle(
        RestoreProductCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        bool restored = product.Restore();

        if (restored)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new RestoreProductResult(
            product.Id.Value,
            product.Status.ToString(),
            restored);
    }
}