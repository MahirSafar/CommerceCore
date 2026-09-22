using Mediator;

namespace CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;

public sealed record ListStorefrontProductsQuery(
    int PageSize = 20,
    Guid? AfterProductId = null,
    Guid? ProductTypeId = null) : IQuery<StorefrontProductPage>
{
    public const int MaximumPageSize = 100;
}

public sealed record StorefrontProductListItem(
    Guid ProductId,
    Guid ProductTypeId,
    string Name,
    decimal BasePriceAmount,
    string Currency);

public sealed record StorefrontProductPage(
    IReadOnlyList<StorefrontProductListItem> Items,
    Guid? NextAfterProductId);
