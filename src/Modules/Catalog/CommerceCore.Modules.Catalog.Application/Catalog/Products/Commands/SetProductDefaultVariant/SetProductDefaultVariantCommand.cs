using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductDefaultVariant;

public sealed record SetProductDefaultVariantCommand(
    Guid ProductId,
    Guid ProductVariantId)
    : ICommand<SetProductDefaultVariantResult?>;

public sealed record SetProductDefaultVariantResult(
    Guid ProductId,
    Guid ProductVariantId,
    bool DefaultChanged);