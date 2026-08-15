using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using Mediator;
namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandHandler(
    ICommerceCoreDbContext dbContext)
    : ICommandHandler<CreateProductCommand, CreateProductResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<CreateProductResult> Handle(
        CreateProductCommand request,
        CancellationToken cancellationToken)
    {
        LanguageCode defaultLanguage = LanguageCode.Create(request.DefaultLanguage);

        IEnumerable<KeyValuePair<LanguageCode, string>> translations = request.NameTranslations.Select(translations =>
            new KeyValuePair<LanguageCode, string>(
                LanguageCode.Create(translations.Key), translations.Value));

        LocalizedText name = LocalizedText.Create(defaultLanguage, translations);

        Money price = Money.Create(request.PriceAmount, request.Currency);

        Product product = Product.Create(name, price);

        _dbContext.Products.Add(product);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new CreateProductResult(product.Id.Value);
    }
}
