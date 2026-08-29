using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.ArchiveProduct;

public sealed class ArchiveProductCommandHandler(
    ICommerceCoreDbContext dbContext,
    IClock clock,
    ICurrentUser currentUser)
    : ICommandHandler<ArchiveProductCommand, ArchiveProductResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IClock _clock = clock;
    private readonly ICurrentUser _currentUser = currentUser;

    public async ValueTask<ArchiveProductResult?> Handle(
        ArchiveProductCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .SingleOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        product.Archive(
            _clock.UtcNow,
            _currentUser.UserId);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new ArchiveProductResult(
            product.Id.Value,
            product.DeletedAtUtc!.Value);
    }
}