using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;

public sealed record ActivateProductCommand(Guid ProductId) : ICommand<ActivateProductResult?>;
public sealed record ActivateProductResult(Guid ProductId, string Status);
