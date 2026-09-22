using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;

public sealed class ListStorefrontProductsQueryHandler(
    IStorefrontProductReader reader) : IQueryHandler<ListStorefrontProductsQuery, StorefrontProductPage>
{
    public async ValueTask<StorefrontProductPage> Handle(
        ListStorefrontProductsQuery query,
        CancellationToken cancellationToken)
    {
        return await reader.ReadAsync(query, cancellationToken);
    }
}
