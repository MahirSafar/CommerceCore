using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;

public sealed record AddProductVariantCommand(
    Guid ProductId,
    string Sku,
    decimal PriceAmount,
    string Currency,
    AttributeValueBag Options,
    bool IsDefault)
    : ICommand<AddProductVariantResult>;

public sealed record AddProductVariantResult(
    Guid ProductId,
    Guid ProductVariantId,
    string Sku,
    string Status,
    bool IsDefault);