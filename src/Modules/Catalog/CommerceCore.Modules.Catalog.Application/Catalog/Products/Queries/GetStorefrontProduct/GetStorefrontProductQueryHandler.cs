using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;

public sealed class GetStorefrontProductQueryHandler(
    IStorefrontProductDetailsReader reader)
    : IQueryHandler<GetStorefrontProductQuery, StorefrontProductDetails?>
{
    public async ValueTask<StorefrontProductDetails?> Handle(
        GetStorefrontProductQuery query,
        CancellationToken cancellationToken)
    {
        return await reader.ReadAsync(query, cancellationToken);
    }
}
