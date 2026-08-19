using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using Mediator;

namespace CommerceCore.Api.Endpoints.V1.ProductTypes;

public static class ProductTypeEndpoints
{
    public sealed record CreateProductTypeRequest(
        string? Code,
        Guid? ParentProductTypeId,
        bool IsAssignable);

    public sealed record CreateProductTypeResponse(Guid ProductTypeId);

    public sealed record DefineAttributeRequest(
        string? Key,
        string? DataType,
        string? Scope,
        bool IsRequired,
        int DisplayOrder,
        decimal? MinimumValue,
        decimal? MaximumValue,
        int? MinimumLength,
        int? MaximumLength,
        string? MeasurementUnitFamily);

    public sealed record DefineAttributeResponse(Guid AttributeDefinitionId);

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

        group.MapPost(
            "{productTypeId:guid}/attributes",
            async (
                Guid productTypeId,
                DefineAttributeRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                if (!TryParseDataType(request.DataType, out AttributeDataType dataType))
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["DataType"] =
                            [
                                "Data type must be one of: text, integer, decimal, boolean, " +
                                "single_select, multi_select, measurement."
                            ]
                        });
                }

                if (!TryParseScope(request.Scope, out AttributeScope scope))
                {
                    return Results.ValidationProblem(
                        new Dictionary<string, string[]>
                        {
                            ["Scope"] =
                            [
                                "Scope must be one of: product_specification, variant_option."
                            ]
                        });
                }

                DefineAttributeResult result = await mediator.Send(
                    new DefineAttributeCommand(
                        productTypeId,
                        request.Key ?? string.Empty,
                        dataType,
                        scope,
                        request.IsRequired,
                        request.DisplayOrder,
                        request.MinimumValue,
                        request.MaximumValue,
                        request.MinimumLength,
                        request.MaximumLength,
                        request.MeasurementUnitFamily),
                    cancellationToken);

                return Results.Created(
                    $"/api/product-types/{productTypeId}/attributes/{result.AttributeDefinitionId}",
                    new DefineAttributeResponse(result.AttributeDefinitionId));
            })
            .WithName("DefineProductTypeAttribute")
            .Produces<DefineAttributeResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }

    private static bool TryParseDataType(
        string? value,
        out AttributeDataType dataType)
    {
        dataType = value?.Trim().ToLowerInvariant() switch
        {
            "text" => AttributeDataType.Text,
            "integer" => AttributeDataType.Integer,
            "decimal" => AttributeDataType.Decimal,
            "boolean" => AttributeDataType.Boolean,
            "single_select" => AttributeDataType.SingleSelect,
            "multi_select" => AttributeDataType.MultiSelect,
            "measurement" => AttributeDataType.Measurement,
            _ => default
        };

        return Enum.IsDefined(dataType);
    }

    private static bool TryParseScope(
        string? value,
        out AttributeScope scope)
    {
        scope = value?.Trim().ToLowerInvariant() switch
        {
            "product_specification" => AttributeScope.ProductSpecification,
            "variant_option" => AttributeScope.VariantOption,
            _ => default
        };

        return Enum.IsDefined(scope);
    }
}