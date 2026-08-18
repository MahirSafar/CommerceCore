using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using Mediator;

namespace CommerceCore.Api.Endpoints.V1.ProductTypes;

public static class ProductTypeEndpoints
{
    public sealed record CreateProductTypeRequest(
        string? Code,
        Guid? ParentProductTypeId,
        bool IsAssignable);

    public sealed record CreateProductTypeResponse(Guid ProductTypeId);

    public static IEndpointRouteBuilder MapProductTypeEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints
            .MapGroup("/api/product-types")
            .WithTags("Product Types");

        group.MapPost(
            string.Empty,
            async (
                CreateProductTypeRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                CreateProductTypeResult result = await mediator.Send(
                    new CreateProductTypeCommand(
                        request.Code ?? string.Empty,
                        request.ParentProductTypeId,
                        request.IsAssignable),
                    cancellationToken);

                return Results.Created(
                    $"/api/product-types/{result.ProductTypeId}",
                    new CreateProductTypeResponse(result.ProductTypeId));
            })
            .WithName("CreateProductType")
            .Produces<CreateProductTypeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
}