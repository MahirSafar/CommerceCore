using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;

public sealed class ChangeProductPriceCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<ChangeProductPriceCommand, ChangeProductPriceResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<ChangeProductPriceResult?> Handle(
        ChangeProductPriceCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        bool changed = product.ChangePrice(
            Money.Create(command.PriceAmount, command.Currency));

        if (changed)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ChangeProductPriceResult(
            product.Id.Value,
            product.Price.Amount,
            product.Price.Currency,
            product.Status.ToString());
    }
}