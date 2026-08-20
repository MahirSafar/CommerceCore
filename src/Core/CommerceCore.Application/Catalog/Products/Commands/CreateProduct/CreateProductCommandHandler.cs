using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Application.Common.Factories;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using Mediator;
using Microsoft.EntityFrameworkCore;

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

        ProductTypeId productTypeId = ProductTypeId.From(command.ProductTypeId);

        ProductType productType = await _dbContext.ProductTypes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == productTypeId,
                cancellationToken)
            ?? throw new ProductDomainException(
                "product.product_type_not_found",
                $"Product type '{productTypeId}' was not found.");

        if (!productType.IsAssignable)
        {
            throw new ProductDomainException(
                "product.product_type_not_assignable",
                $"Product type '{productType.Code}' cannot be assigned to products.");
        }

        Product product = Product.Create(
            name,
            price,
            productTypeId,
            _clock.UtcNow);

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id.Value);
    }
}