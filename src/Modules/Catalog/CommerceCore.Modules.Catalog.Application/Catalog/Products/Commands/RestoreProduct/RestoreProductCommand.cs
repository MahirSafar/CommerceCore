using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.RestoreProduct;

public sealed record RestoreProductCommand(Guid ProductId) : ICommand<RestoreProductResult?>;

public sealed record RestoreProductResult(Guid ProductId, string Status, bool Restored);