using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductVariantById;

public sealed record GetProductVariantByIdQuery(
    Guid ProductId,
    Guid ProductVariantId)
    : IQuery<GetProductVariantByIdResult?>;

public sealed record GetProductVariantByIdResult(
    Guid ProductId,
    Guid ProductVariantId,
    string Sku,
    decimal PriceAmount,
    string Currency,
    string Status,
    bool IsDefault,
    AttributeValueBag Options);