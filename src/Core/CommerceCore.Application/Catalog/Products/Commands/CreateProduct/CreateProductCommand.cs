using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed record CreateProductCommand(
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    decimal PriceAmount,
    string Currency)
    : ICommand<CreateProductResult>;

public sealed record CreateProductResult(Guid ProductId);


