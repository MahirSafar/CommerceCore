using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductVariants;

public sealed record GetProductVariantsQuery(Guid ProductId)
    : IQuery<GetProductVariantsResult?>;

public sealed record GetProductVariantsResult(
    Guid ProductId,
    IReadOnlyList<GetProductVariantListItem> Variants);

public sealed record GetProductVariantListItem(
    Guid ProductVariantId,
    string Sku,
    decimal PriceAmount,
    string Currency,
    string Status,
    bool IsDefault,
    CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag Options);