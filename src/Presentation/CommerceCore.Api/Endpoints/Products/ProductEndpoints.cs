using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Catalog.Products.Queries;
using Mediator;

namespace CommerceCore.Api.Endpoints.Products;

public static class ProductEndpoints
{
    public sealed record CreateProductRequest(
        string? DefaultLanguage,
        Dictionary<string, string>? NameTranslations,
        decimal PriceAmount,
        string? Currency);

    public sealed record CreateProductResponse(Guid ProductId);
    public static IEndpointRouteBuilder MapProductEndpoints(this IEndpointRouteBuilder endpoints)
    {
        RouteGroupBuilder group = endpoints
            .MapGroup("/api/products")
            .WithTags("Products");

        group.MapPost(
            string.Empty,
            async (
                CreateProductRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                CreateProductCommand command = new(
                    request.DefaultLanguage ?? string.Empty,
                    request.NameTranslations ?? [],
                    request.PriceAmount,
                    request.Currency ?? string.Empty);

                CreateProductResult result = await mediator.Send(
                    command,
                    cancellationToken);

                return Results.CreatedAtRoute(
                    "GetProductById",
                    new { productId = result.ProductId },
                    new CreateProductResponse(result.ProductId));
            })
        .WithName("CreateProduct")
        .Produces<CreateProductResponse>(
            StatusCodes.Status201Created)
        .ProducesValidationProblem(
            StatusCodes.Status400BadRequest)
        .ProducesProblem(
            StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(
            StatusCodes.Status500InternalServerError);

        group.MapGet(
            "{productId:guid}",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new GetProductByIdQuery(productId),
                    cancellationToken);

                if (result is null)
                    return Results.NotFound();

                return Results.Ok(
                    new GetProductResponse(
                        result.ProductId,
                        result.DefaultLanguage,
                        result.NameTranslations,
                        result.PriceAmount,
                        result.Currency,
                        result.Status));
            })
        .WithName("GetProductById")
        .Produces<GetProductResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        return endpoints;
    }

    public sealed record GetProductResponse(
    Guid ProductId,
    string DefaultLanguage,
    IReadOnlyDictionary<string, string> NameTranslations,
    decimal PriceAmount,
    string Currency,
    string Status);
}
