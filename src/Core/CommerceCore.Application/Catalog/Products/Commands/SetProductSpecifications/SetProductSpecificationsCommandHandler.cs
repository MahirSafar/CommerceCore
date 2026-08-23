using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.Services;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;

public sealed class SetProductSpecificationsCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeEffectiveSchemaReader schemaReader,
    ICatalogSchemaValidator schemaValidator)
    : ICommandHandler<
        SetProductSpecificationsCommand,
        SetProductSpecificationsResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeEffectiveSchemaReader _schemaReader =
        schemaReader;
    private readonly ICatalogSchemaValidator _schemaValidator =
        schemaValidator;

    public async ValueTask<SetProductSpecificationsResult> Handle(
        SetProductSpecificationsCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product product = await _dbContext.Products
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken)
            ?? throw new ProductDomainException(
                "product.not_found",
                $"Product '{productId}' was not found.");

        EffectiveProductTypeSchema schema = await _schemaReader.GetAsync(
            product.ProductTypeId,
            cancellationToken)
            ?? throw new ProductDomainException(
                "product.effective_schema_not_found",
                $"Effective schema for product type '{product.ProductTypeId}' was not found.");

        AttributeValueBag proposedSpecifications =
            MaterializeSpecifications(command.Specifications, schema);

        CatalogSchemaValidationResult validationResult =
            _schemaValidator.ValidateProductSpecifications(
                product.Specifications,
                proposedSpecifications,
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

        bool changed = product.ApplyValidatedSpecifications(
            proposedSpecifications,
            schema.EffectiveSchemaVersion);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new SetProductSpecificationsResult(
            product.Id.Value,
            product.ValidatedAgainstVersion,
            changed);
    }

    private static AttributeValueBag MaterializeSpecifications(
        ProductSpecificationsInput input,
        EffectiveProductTypeSchema schema)
    {
        Dictionary<AttributeKey, EffectiveAttributeDefinition> definitions =
            schema.Attributes.ToDictionary(definition => definition.Key);

        AttributeValueBag result = AttributeValueBag.Empty;

        foreach ((AttributeKey key, ProductSpecificationInputValue inputValue)
                 in input.Values)
        {
            if (inputValue is ProductSpecificationInputValue.Typed typed)
            {
                result = result.With(key, typed.Value);
                continue;
            }

            if (inputValue is not ProductSpecificationInputValue.Measurement
                measurement)
            {
                throw new InvalidOperationException(
                    $"Unsupported specification input for '{key.Value}'.");
            }

            if (!definitions.TryGetValue(
                    key,
                    out EffectiveAttributeDefinition? definition))
            {
                throw CreateValidationException(
                    key,
                    "catalog_schema.unknown_attribute",
                    $"Attribute '{key.Value}' is not defined by this product type.");
            }

            if (definition.Scope != AttributeScope.ProductSpecification)
            {
                throw CreateValidationException(
                    key,
                    "catalog_schema.invalid_scope",
                    $"Attribute '{key.Value}' cannot be used as a product specification.");
            }

            if (definition.DataType != AttributeDataType.Measurement)
            {
                throw CreateValidationException(
                    key,
                    "catalog_schema.attribute_type_mismatch",
                    $"Attribute '{key.Value}' must have type '{definition.DataType}'.");
            }

            if (definition.MeasurementUnitFamily is not MeasurementUnitFamily family)
            {
                throw CreateValidationException(
                    key,
                    "catalog_schema.invalid_measurement_definition",
                    $"Measurement attribute '{key.Value}' has no unit family.");
            }

            AttributeValue.Measurement? normalized;

            try
            {
                if (!MeasurementUnitNormalizer.TryNormalize(
                        family,
                        measurement.Value,
                        measurement.Unit,
                        out normalized) ||
                    normalized is null)
                {
                    throw CreateValidationException(
                        key,
                        "catalog_schema.measurement_unit_not_supported",
                        $"Unit '{measurement.Unit}' is not supported for '{key.Value}'.");
                }
            }
            catch (OverflowException)
            {
                throw CreateValidationException(
                    key,
                    "catalog_schema.measurement_value_out_of_range",
                    $"Measurement value for '{key.Value}' is outside the supported range.");
            }

            result = result.With(key, normalized);
        }

        return result;
    }

    private static ValidationException CreateValidationException(
        AttributeKey key,
        string errorCode,
        string message) => new(
        [
            new ValidationFailure(
                $"specifications.{key.Value}",
                message)
            {
                ErrorCode = errorCode
            }
        ]);
}