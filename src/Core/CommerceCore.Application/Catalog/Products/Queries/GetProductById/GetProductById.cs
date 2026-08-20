using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductById;

public sealed record GetProductByIdQuery(Guid ProductId)
    : IQuery<GetProductByIdResult?>;

public sealed record GetProductByIdResult(
    Guid ProductId,
    Guid ProductTypeId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    decimal PriceAmount,
    string Currency,
    string Status);