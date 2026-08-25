using CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;
using CommerceCore.Application.Catalog.Products.Commands.ActivateProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.ArchiveProduct;
using CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;
using CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;
using CommerceCore.Application.Catalog.Products.Commands.CreateProduct;
using CommerceCore.Application.Catalog.Products.Commands.DeactivateProduct;
using CommerceCore.Application.Catalog.Products.Commands.DeactivateProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.RestoreProduct;
using CommerceCore.Application.Catalog.Products.Commands.SetProductDefaultVariant;
using CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;
using CommerceCore.Application.Catalog.Products.Queries.GetProductById;
using CommerceCore.Application.Catalog.Products.Queries.GetProductVariantById;
using CommerceCore.Application.Catalog.Products.Queries.GetProductVariants;
using Mediator;
using System.Text.Json;

namespace CommerceCore.Api.Endpoints.V1.Products;

public static class ProductEndpoints
{
    public sealed record CreateProductRequest(
        Guid ProductTypeId,
        string? DefaultLanguage,
        Dictionary<string, string>? NameTranslations,
        decimal PriceAmount,
        string? Currency);

    public sealed record CreateProductResponse(Guid ProductId);
    public sealed record AddProductVariantRequest(
    string? Sku,
    decimal PriceAmount,
    string? Currency,
    JsonElement Options,
    bool IsDefault);

    public sealed record AddProductVariantResponse(
        Guid ProductId,
        Guid ProductVariantId,
        string Sku,
        string Status,
        bool IsDefault);

    public sealed record GetProductVariantResponse(
        Guid ProductId,
        Guid ProductVariantId,
        string Sku,
        decimal PriceAmount,
        string Currency,
        string Status,
        bool IsDefault,
        JsonElement Options);
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
                    request.ProductTypeId,
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
                        result.ProductTypeId,
                        result.DefaultLanguage,
                        result.NameTranslations,
                        result.PriceAmount,
                        result.Currency,
                        result.Status,
                        AttributeValueBagResponseSerializer.Serialize(
                            result.Specifications),
                        result.ValidatedAgainstVersion));
            })
        .WithName("GetProductById")
        .Produces<GetProductResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet(
            "{productId:guid}/variants",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                GetProductVariantsResult? result = await mediator.Send(
                    new GetProductVariantsQuery(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                GetProductVariantListItemResponse[] variants = result.Variants
                    .Select(item => new GetProductVariantListItemResponse(
                        item.ProductVariantId,
                        item.Sku,
                        item.PriceAmount,
                        item.Currency,
                        item.Status,
                        item.IsDefault,
                        AttributeValueBagResponseSerializer.Serialize(
                            item.Options)))
                    .ToArray();

                return Results.Ok(
                    new GetProductVariantsResponse(
                        result.ProductId,
                        variants));
            })
        .WithName("GetProductVariants")
        .Produces<GetProductVariantsResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapGet(
            "{productId:guid}/variants/{productVariantId:guid}",
            async (
                Guid productId,
                Guid productVariantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                GetProductVariantByIdResult? result = await mediator.Send(
                    new GetProductVariantByIdQuery(
                        productId,
                        productVariantId),
                    cancellationToken);

                if (result is null)
                    return ProductVariantNotFound();

                return Results.Ok(
                    new GetProductVariantResponse(
                        result.ProductId,
                        result.ProductVariantId,
                        result.Sku,
                        result.PriceAmount,
                        result.Currency,
                        result.Status,
                        result.IsDefault,
                        AttributeValueBagResponseSerializer.Serialize(
                            result.Options)));
            })
        .WithName("GetProductVariantById")
        .Produces<GetProductVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(
            "{productId:guid}/variants/{productVariantId:guid}/activate",
            async (
                Guid productId,
                Guid productVariantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                ActivateProductVariantResult? result = await mediator.Send(
                    new ActivateProductVariantCommand(
                        productId,
                        productVariantId),
                    cancellationToken);

                if (result is null)
                    return ProductVariantNotFound();

                return Results.Ok(
                    new ActivateProductVariantResponse(
                        result.ProductId,
                        result.ProductVariantId,
                        result.Status,
                        result.Activated));
            })
        .WithName("ActivateProductVariant")
        .Produces<ActivateProductVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(
            "{productId:guid}/variants/{productVariantId:guid}/deactivate",
            async (
                Guid productId,
                Guid productVariantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                DeactivateProductVariantResult? result = await mediator.Send(
                    new DeactivateProductVariantCommand(
                        productId,
                        productVariantId),
                    cancellationToken);

                if (result is null)
                    return ProductVariantNotFound();

                return Results.Ok(
                    new DeactivateProductVariantResponse(
                        result.ProductId,
                        result.ProductVariantId,
                        result.Status,
                        result.Deactivated));
            })
        .WithName("DeactivateProductVariant")
        .Produces<DeactivateProductVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut(
            "{productId:guid}/variants/{productVariantId:guid}/default",
            async (
                Guid productId,
                Guid productVariantId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                SetProductDefaultVariantResult? result = await mediator.Send(
                    new SetProductDefaultVariantCommand(
                        productId,
                        productVariantId),
                    cancellationToken);

                if (result is null)
                    return ProductVariantNotFound();

                return Results.Ok(
                    new SetProductDefaultVariantResponse(
                        result.ProductId,
                        result.ProductVariantId,
                        result.DefaultChanged));
            })
        .WithName("SetProductDefaultVariant")
        .Produces<SetProductDefaultVariantResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPost(
            "{productId:guid}/variants",
            async (
                Guid productId,
                AddProductVariantRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var options = VariantOptionsRequestParser.Parse(
                    request.Options);

                AddProductVariantResult result = await mediator.Send(
                    new AddProductVariantCommand(
                        productId,
                        request.Sku ?? string.Empty,
                        request.PriceAmount,
                        request.Currency ?? string.Empty,
                        options,
                        request.IsDefault),
                    cancellationToken);

                return Results.CreatedAtRoute(
                    "GetProductVariantById",
                    new
                    {
                        productId = result.ProductId,
                        productVariantId = result.ProductVariantId
                    },
                    new AddProductVariantResponse(
                        result.ProductId,
                        result.ProductVariantId,
                        result.Sku,
                        result.Status,
                        result.IsDefault));
            })
        .WithName("AddProductVariant")
        .Produces<AddProductVariantResponse>(StatusCodes.Status201Created)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
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


        group.MapPost(
            "{productId:guid}/restore",
            async (
                Guid productId,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new RestoreProductCommand(productId),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new RestoreProductResponse(
                        result.ProductId,
                        result.Status,
                        result.Restored));
            })
        .WithName("RestoreProduct")
        .Produces<RestoreProductResponse>(StatusCodes.Status200OK)
        .ProducesProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut(
            "{productId:guid}/price",
            async (
                Guid productId,
                ChangeProductPriceRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var result = await mediator.Send(
                    new ChangeProductPriceCommand(
                        productId,
                        request.PriceAmount,
                        request.Currency ?? string.Empty),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new ProductPriceResponse(
                        result.ProductId,
                        result.PriceAmount,
                        result.Currency,
                        result.Status));
            })
        .WithName("ChangeProductPrice")
        .Produces<ProductPriceResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut(
            "{productId:guid}/name",
            async (
                Guid productId,
                ChangeProductNameRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                ChangeProductNameResult? result = await mediator.Send(
                    new ChangeProductNameCommand(
                        productId,
                        request.DefaultLanguage ?? string.Empty,
                        request.NameTranslations ?? []),
                    cancellationToken);

                if (result is null)
                    return ProductNotFound();

                return Results.Ok(
                    new ProductNameResponse(
                        result.ProductId,
                        result.DefaultLanguage,
                        result.NameTranslations,
                        result.Status));
            })
        .WithName("ChangeProductName")
        .Produces<ProductNameResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status409Conflict)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        group.MapPut(
            "{productId:guid}/specifications",
            async (
                Guid productId,
                SetProductSpecificationsRequest request,
                IMediator mediator,
                CancellationToken cancellationToken) =>
            {
                var specifications = AttributeValueBagRequestParser.Parse(
                    request.Specifications);

                SetProductSpecificationsResult result = await mediator.Send(
                    new SetProductSpecificationsCommand(
                        productId,
                        specifications),
                    cancellationToken);

                return Results.Ok(
                    new SetProductSpecificationsResponse(
                        result.ProductId,
                        result.ValidatedAgainstVersion,
                        result.Changed));
            })
        .WithName("SetProductSpecifications")
        .Produces<SetProductSpecificationsResponse>(StatusCodes.Status200OK)
        .ProducesValidationProblem(StatusCodes.Status400BadRequest)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity)
        .ProducesProblem(StatusCodes.Status500InternalServerError);

        return endpoints;
    }
    public sealed record SetProductSpecificationsRequest(
        JsonElement Specifications);

    public sealed record SetProductSpecificationsResponse(
        Guid ProductId,
        long ValidatedAgainstVersion,
        bool Changed);
    public sealed record ActivateProductResponse(Guid ProductId, string Status);
    public sealed record DeactivateProductResponse(Guid ProductId, string Status);
    public sealed record ArchiveProductResponse(Guid ProductId, DateTimeOffset ArchivedAtUtc);
    public sealed record RestoreProductResponse(Guid ProductId, string Status, bool Restored);
    public sealed record ChangeProductPriceRequest(decimal PriceAmount, string? Currency);
    public sealed record ProductPriceResponse(Guid ProductId, decimal PriceAmount, string Currency, string Status);
    public sealed record ChangeProductNameRequest(string? DefaultLanguage, Dictionary<string, string>? NameTranslations);
    public sealed record ProductNameResponse(Guid ProductId, string DefaultLanguage, IReadOnlyDictionary<string, string> NameTranslations, string Status);
    public sealed record GetProductResponse(
        Guid ProductId,
        Guid ProductTypeId,
        string DefaultLanguage,
        IReadOnlyDictionary<string, string> NameTranslations,
        decimal PriceAmount,
        string Currency,
        string Status,
        JsonElement Specifications,
        long ValidatedAgainstVersion);

    public sealed record ActivateProductVariantResponse(
        Guid ProductId,
        Guid ProductVariantId,
        string Status,
        bool Activated);

    public sealed record DeactivateProductVariantResponse(
        Guid ProductId,
        Guid ProductVariantId,
        string Status,
        bool Deactivated);

    public sealed record SetProductDefaultVariantResponse(
    Guid ProductId,
    Guid ProductVariantId,
    bool DefaultChanged);

    public sealed record GetProductVariantsResponse(
    Guid ProductId,
    IReadOnlyList<GetProductVariantListItemResponse> Variants);

    public sealed record GetProductVariantListItemResponse(
        Guid ProductVariantId,
        string Sku,
        decimal PriceAmount,
        string Currency,
        string Status,
        bool IsDefault,
        JsonElement Options);

    private static IResult ProductVariantNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        type: "/problems/product-variant-not-found",
        title: "Product or product variant was not found.");
    private static IResult ProductNotFound() => Results.Problem(
            statusCode: StatusCodes.Status404NotFound,
            type: "/problems/product-not-found",
            title: "Product was not found.");


}
