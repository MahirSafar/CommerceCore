using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;

public sealed class ActivateProductCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeEffectiveSchemaReader schemaReader,
    ICatalogSchemaValidator schemaValidator)
    : ICommandHandler<ActivateProductCommand, ActivateProductResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeEffectiveSchemaReader _schemaReader =
        schemaReader;
    private readonly ICatalogSchemaValidator _schemaValidator =
        schemaValidator;

    public async ValueTask<ActivateProductResult?> Handle(
        ActivateProductCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        EffectiveProductTypeSchema schema = await _schemaReader.GetAsync(
            product.ProductTypeId,
            cancellationToken)
            ?? throw new ProductDomainException(
                "product.effective_schema_not_found",
                $"Effective schema for product type '{product.ProductTypeId}' was not found.");

        CatalogSchemaValidationResult validationResult =
            _schemaValidator.ValidateProductSpecifications(
                product.Specifications,
                product.Specifications,
                schema);

        if (!validationResult.IsValid)
        {
            IEnumerable<ValidationFailure> failures = validationResult.Errors
                .Select(error => new ValidationFailure(
                    $"specifications.{error.AttributeKey.Value}",
                    error.Message)
                {
                    ErrorCode = error.Code
                });

            throw new ValidationException(failures);
        }

        bool schemaVersionUpdated = product.ApplyValidatedSpecifications(
            product.Specifications,
            schema.EffectiveSchemaVersion);

        bool activated = product.Activate();

        if (schemaVersionUpdated || activated)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateProductResult(
            product.Id.Value,
            product.Status.ToString());
    }
}