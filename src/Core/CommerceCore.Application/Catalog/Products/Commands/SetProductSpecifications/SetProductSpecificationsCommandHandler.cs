using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
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
            command.Specifications.ToTypedBag();

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
}