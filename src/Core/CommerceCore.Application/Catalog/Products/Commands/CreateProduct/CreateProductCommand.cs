using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    Guid ProductTypeId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    decimal PriceAmount,
    string Currency)
    : ICommand<CreateProductResult>;

public sealed record CreateProductResult(Guid ProductId);


