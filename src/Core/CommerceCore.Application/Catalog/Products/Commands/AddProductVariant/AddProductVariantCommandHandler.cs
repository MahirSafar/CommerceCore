using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Common.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;

public sealed class AddProductVariantCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeEffectiveSchemaReader schemaReader,
    ICatalogSchemaValidator schemaValidator)
    : ICommandHandler<
        AddProductVariantCommand,
        AddProductVariantResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeEffectiveSchemaReader _schemaReader =
        schemaReader;
    private readonly ICatalogSchemaValidator _schemaValidator =
        schemaValidator;

    public async ValueTask<AddProductVariantResult> Handle(
        AddProductVariantCommand command,
        CancellationToken cancellationToken)
    {
        ProductId productId = ProductId.From(command.ProductId);

        Product product = await _dbContext.Products
            .Include(item => item.Variants)
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

        CatalogSchemaValidationResult validationResult =
            _schemaValidator.ValidateVariantOptions(
                AttributeValueBag.Empty,
                command.Options,
                schema);

        if (!validationResult.IsValid)
        {
            IEnumerable<ValidationFailure> failures = validationResult.Errors
                .Select(error => new ValidationFailure(
                    $"options.{error.AttributeKey.Value}",
                    error.Message)
                {
                    ErrorCode = error.Code
                });

            throw new ValidationException(failures);
        }

        ProductVariant variant = product.AddVariant(
            VariantSku.Create(command.Sku),
            Money.Create(command.PriceAmount, command.Currency),
            command.Options,
            command.IsDefault);

        await _dbContext.SaveChangesAsync(cancellationToken);

        return new AddProductVariantResult(
            product.Id.Value,
            variant.Id.Value,
            variant.Sku.Value,
            variant.Status.ToString(),
            variant.IsDefault);
    }
}