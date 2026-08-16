using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Application.Common.Factories;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    ICommerceCoreDbContext dbContext,
    IClock clock)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;

    public async ValueTask<CreateProductResult> Handle(
        CreateProductCommand command,
        CancellationToken cancellationToken)
    {
        LocalizedText name = LocalizedTextFactory.Create(
            command.DefaultLanguage,
            command.NameTranslations);

        Money price = Money.Create(
            command.PriceAmount,
            command.Currency);

        Product product = Product.Create(
            name,
            price,
            _clock.UtcNow);

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id.Value);
    }
}