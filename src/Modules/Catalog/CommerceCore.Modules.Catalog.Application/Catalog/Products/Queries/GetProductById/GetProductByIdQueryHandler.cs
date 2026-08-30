using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryHandler(
    ICommerceCoreDbContext dbContext)
    : IQueryHandler<GetProductByIdQuery, GetProductByIdResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;

    public async ValueTask<GetProductByIdResult?> Handle(
        GetProductByIdQuery query,
        CancellationToken cancellationToken)
    {
        var productId = ProductId.From(query.ProductId);

        var product = await _dbContext.Products
            .AsNoTracking()
            .SingleOrDefaultAsync(
                product => product.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        return new GetProductByIdResult(
            product.Id.Value,
            product.ProductTypeId.Value,
            product.Name.DefaultLanguage.Value,
            product.Name.Translations.ToDictionary(
                translation => translation.Key.Value,
                translation => translation.Value,
                StringComparer.Ordinal),
            product.Price.Amount,
            product.Price.Currency,
            product.Status.ToString(),
            product.Specifications,
            product.ValidatedAgainstVersion);
    }
}