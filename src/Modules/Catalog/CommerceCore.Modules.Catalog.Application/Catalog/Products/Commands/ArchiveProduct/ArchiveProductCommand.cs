using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.ArchiveProduct;

public sealed record ArchiveProductCommand(Guid ProductId) : ICommand<ArchiveProductResult?>;

public sealed record ArchiveProductResult(Guid ProductId, DateTimeOffset ArchivedAtUtc);
