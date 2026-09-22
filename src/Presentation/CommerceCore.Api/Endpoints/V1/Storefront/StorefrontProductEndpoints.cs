using CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;
using CommerceCore.Platform.Identity;
using Mediator;

namespace CommerceCore.Api.Endpoints.V1.Storefront;

public static class StorefrontProductEndpoints
{
    public static IEndpointRouteBuilder MapStorefrontProductEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/storefront/products",
            async (
                int? pageSize,
                Guid? afterProductId,
                Guid? productTypeId,
                HttpContext context,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                var query = new ListStorefrontProductsQuery(
                    PageSize: pageSize ?? 20,
                    AfterProductId: afterProductId,
                    ProductTypeId: productTypeId);

                StorefrontProductPage result = await mediator.Send(
                    query,
                    cancellationToken);

                ProductResponse[] items = result.Items
                    .Select(item => new ProductResponse(
                        item.ProductId,
                        item.ProductTypeId,
                        item.Name,
                        item.BasePriceAmount,
                        item.Currency))
                    .ToArray();

                return Results.Ok(new ProductsResponse(
                    items,
                    result.NextAfterProductId));
            })
            .WithName("ListStorefrontProducts")
            .WithTags("Storefront")
            .WithMetadata(PublicStorefrontReadMetadata.Instance)
            .AllowAnonymous()
            .Produces<ProductsResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    public sealed record ProductResponse(
        Guid ProductId,
        Guid ProductTypeId,
        string Name,
        decimal BasePriceAmount,
        string Currency);

    public sealed record ProductsResponse(
        IReadOnlyList<ProductResponse> Items,
        Guid? NextAfterProductId);
}
