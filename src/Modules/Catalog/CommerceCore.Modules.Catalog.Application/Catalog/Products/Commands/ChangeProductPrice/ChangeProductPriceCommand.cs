using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;

public sealed record ChangeProductPriceCommand(
    Guid ProductId,
    decimal PriceAmount,
    string Currency)
    : ICommand<ChangeProductPriceResult?>;

public sealed record ChangeProductPriceResult(
    Guid ProductId,
    decimal PriceAmount,
    string Currency,
    string Status);