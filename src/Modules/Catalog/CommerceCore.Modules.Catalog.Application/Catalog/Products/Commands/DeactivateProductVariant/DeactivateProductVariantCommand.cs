using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProductVariant;

public sealed record DeactivateProductVariantCommand(
    Guid ProductId,
    Guid ProductVariantId)
    : ICommand<DeactivateProductVariantResult?>;

public sealed record DeactivateProductVariantResult(
    Guid ProductId,
    Guid ProductVariantId,
    string Status,
    bool Deactivated);