using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProduct;

public sealed record DeactivateProductCommand(Guid ProductId) : ICommand<DeactivateProductResult?>;

public sealed record DeactivateProductResult(Guid ProductId, string Status);
