using CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;

namespace CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;

public interface IStorefrontProductDetailsReader
{
    Task<StorefrontProductDetails?> ReadAsync(
        GetStorefrontProductQuery query,
        CancellationToken cancellationToken);
}
