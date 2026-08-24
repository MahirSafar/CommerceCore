using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProductVariant;

public sealed record ActivateProductVariantCommand(
    Guid ProductId,
    Guid ProductVariantId)
    : ICommand<ActivateProductVariantResult?>;

public sealed record ActivateProductVariantResult(
    Guid ProductId,
    Guid ProductVariantId,
    string Status,
    bool Activated);