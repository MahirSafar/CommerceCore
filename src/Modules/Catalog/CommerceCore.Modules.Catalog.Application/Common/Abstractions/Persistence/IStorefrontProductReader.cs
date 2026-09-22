using CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;

namespace CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;

public interface IStorefrontProductReader
{
    Task<StorefrontProductPage> ReadAsync(
        ListStorefrontProductsQuery query,
        CancellationToken cancellationToken);
}
