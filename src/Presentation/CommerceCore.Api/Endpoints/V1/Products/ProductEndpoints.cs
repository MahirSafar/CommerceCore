using CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;
using CommerceCore.Application.Catalog.Products.Commands.ArchiveProduct;
using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Catalog.Products.Commands.DeactivateProduct;
using CommerceCore.Application.Catalog.Products.Queries.GetProductById;
using Mediator;
using static CommerceCore.Api.Endpoints.V1.Products.ProductEndpoints;

namespace CommerceCore.Api.Endpoints.V1.Products;

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
                GetProductByIdResult? result = await mediator.Send(
                    new GetProductByIdQuery(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

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

        group.MapPost(
            "{productId:guid}/activate",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                ActivateProductResult? result = await mediator.Send(
                    new ActivateProductCommand(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new ActivateProductResponse(
                        result.ProductId,
                        result.Status));
            })
        .WithName("ActivateProduct")
        .Produces<ActivateProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(
            "{productId:guid}/deactivate",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                DeactivateProductResult? result = await mediator.Send(
                    new DeactivateProductCommand(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new DeactivateProductResponse(
                        result.ProductId,
                        result.Status));
            })
        .WithName("DeactivateProduct")
        .Produces<DeactivateProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(
            "{productId:guid}/archive",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                ArchiveProductResult? result = await mediator.Send(
                    new ArchiveProductCommand(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new ArchiveProductResponse(
                        result.ProductId,
                        result.ArchivedAtUtc));
            })
        .WithName("ArchiveProduct")
        .Produces<ArchiveProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);
        return endpoints;
    }
    public sealed record ActivateProductResponse(Guid ProductId, string Status);
    public sealed record DeactivateProductResponse(Guid ProductId, string Status);
    public sealed record ArchiveProductResponse(Guid ProductId, DateTimeOffset ArchivedAtUtc);
    public sealed record GetProductResponse(
        Guid ProductId,
        string DefaultLanguage,
        IReadOnlyDictionary<string, string> NameTranslations,
        decimal PriceAmount,
        string Currency,
        string Status);

    private static IResult ProductNotFound() => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: "/problems/product-not-found",
            title: "Product was not found.");
}
