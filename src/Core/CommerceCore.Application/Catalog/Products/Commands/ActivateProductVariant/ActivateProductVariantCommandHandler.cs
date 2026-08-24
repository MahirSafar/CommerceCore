using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProductVariant;

public sealed class ActivateProductVariantCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeEffectiveSchemaReader schemaReader,
    ICatalogSchemaValidator schemaValidator)
    : ICommandHandler<
        ActivateProductVariantCommand,
        ActivateProductVariantResult?>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeEffectiveSchemaReader _schemaReader =
        schemaReader;
    private readonly ICatalogSchemaValidator _schemaValidator =
        schemaValidator;

    public async ValueTask<ActivateProductVariantResult?> Handle(
        ActivateProductVariantCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product? product = await _dbContext.Products
            .Include(item => item.Variants)
            .SingleOrDefaultAsync(
                item => item.Id == productId,
                cancellationToken);

        if (product is null)
            return null;

        ProductVariant? variant = product.Variants.SingleOrDefault(
            item => item.Id.Value == command.ProductVariantId);

        if (variant is null)
            return null;

        EffectiveProductTypeSchema schema = await _schemaReader.GetAsync(
            product.ProductTypeId,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Effective schema for product type '{product.ProductTypeId}' was not found.");

        CatalogSchemaValidationResult validationResult =
            _schemaValidator.ValidateVariantOptions(
                variant.Options,
                variant.Options,
                schema);

        if (!validationResult.IsValid)
        {
            IEnumerable<ValidationFailure> failures = validationResult.Errors
                .Select(error => new ValidationFailure(
                    $"variants.{variant.Id.Value}.options.{error.AttributeKey.Value}",
                    error.Message)
                {
                    ErrorCode = error.Code
                });

            throw new ValidationException(failures);
        }

        bool activated = product.ActivateVariant(variant.Id);

        if (activated)
            await _dbContext.SaveChangesAsync(cancellationToken);

        return new ActivateProductVariantResult(
            product.Id.Value,
            variant.Id.Value,
            variant.Status.ToString(),
            activated);
    }
}