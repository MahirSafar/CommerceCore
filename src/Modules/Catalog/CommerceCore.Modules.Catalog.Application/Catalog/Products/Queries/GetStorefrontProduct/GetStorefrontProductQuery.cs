using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;

public sealed record GetStorefrontProductQuery(Guid ProductId)
    : IQuery<StorefrontProductDetails?>;

public sealed record StorefrontProductDetails(
    Guid ProductId,
    Guid ProductTypeId,
    string Name,
    decimal BasePriceAmount,
    string Currency,
    IReadOnlyList<StorefrontVariantDetails> Variants);

public sealed record StorefrontVariantDetails(
    Guid ProductVariantId,
    string Sku,
    decimal BasePriceAmount,
    string Currency,
    bool IsDefault,
    IReadOnlyDictionary<string, string> Options);
