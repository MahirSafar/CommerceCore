using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries;

public sealed record GetProductByIdQuery(Guid ProductId)
    : IQuery<GetProductByIdResult?>;

public sealed record GetProductByIdResult(
    Guid ProductId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    decimal PriceAmount,
    string Currency,
    string Status);