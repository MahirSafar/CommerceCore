using CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;
using CommerceCore.Platform.Identity;
using Mediator;

namespace CommerceCore.Api.Endpoints.V1.Storefront;

public static class StorefrontProductDetailsEndpoints
{
    public static IEndpointRouteBuilder MapStorefrontProductDetailsEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet(
            "/api/storefront/products/{productId:guid}",
            async Task<IResult> (
                Guid productId,
                HttpContext context,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                context.Response.Headers.CacheControl = "no-store";

                StorefrontProductDetails? result = await mediator.Send(
                    new GetStorefrontProductQuery(productId),
                    cancellationToken);

                if (result is null)
                {
                    return Results.NotFound();
                }

                VariantResponse[] variants = result.Variants
                    .Select(variant => new VariantResponse(
                        variant.ProductVariantId,
                        variant.Sku,
                        variant.BasePriceAmount,
                        variant.Currency,
                        variant.IsDefault))
                    .ToArray();

                return Results.Ok(new ProductDetailsResponse(
                    result.ProductId,
                    result.ProductTypeId,
                    result.Name,
                    result.BasePriceAmount,
                    result.Currency,
                    variants));
            })
            .WithName("GetStorefrontProduct")
            .WithTags("Storefront")
            .WithMetadata(PublicStorefrontReadMetadata.Instance)
            .AllowAnonymous()
            .Produces<ProductDetailsResponse>(StatusCodes.Status200OK)
            .Produces(StatusCodes.Status404NotFound)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest);

        return endpoints;
    }

    public sealed record ProductDetailsResponse(
        Guid ProductId,
        Guid ProductTypeId,
        string Name,
        decimal BasePriceAmount,
        string Currency,
        IReadOnlyList<VariantResponse> Variants);

    public sealed record VariantResponse(
        Guid ProductVariantId,
        string Sku,
        decimal BasePriceAmount,
        string Currency,
        bool IsDefault);
}
