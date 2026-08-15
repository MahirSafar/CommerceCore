using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
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

                return Results.Created(
                    $"/api/products/{result.ProductId}",
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

        return endpoints;
    }
}
