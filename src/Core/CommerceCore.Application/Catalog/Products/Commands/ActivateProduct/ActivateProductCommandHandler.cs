using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Metadata.Ecma335;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;

public sealed class ActivateProductCommandHandler(ICommerceCoreDbContext dbContext) 
    : ICommandHandler<ActivateProductCommand, ActivateProductResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    public async ValueTask<ActivateProductResult?> Handle(ActivateProductCommand command, CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products.SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);

        if (product is null)
            return null;

        bool changed = product.Activate();

        if(changed)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateProductResult(product.Id.Value, product.Status.ToString());
    }


}
