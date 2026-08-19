using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;

public sealed class DefineAttributeCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeSchemaCoordinator schemaCoordinator,
    IAttributeDefinitionRegistry attributeDefinitionRegistry)
    : ICommandHandler<DefineAttributeCommand, DefineAttributeResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeSchemaCoordinator _schemaCoordinator = schemaCoordinator;
    private readonly IAttributeDefinitionRegistry _attributeDefinitionRegistry = attributeDefinitionRegistry;
    public async ValueTask<DefineAttributeResult> Handle(
        DefineAttributeCommand command,
        CancellationToken cancellationToken)
    {
        ProductTypeId productTypeId = ProductTypeId.From(command.ProductTypeId);

        ProductType productType = await _dbContext.ProductTypes
            .Include(item => item.AttributeDefinitions)
            .SingleOrDefaultAsync(
                item => item.Id == productTypeId,
                cancellationToken)
            ?? throw new ProductTypeDomainException(
                "product_type.not_found",
                $"Product type '{productTypeId}' was not found.");

        AttributeKey key = AttributeKey.Create(command.Key);

        MeasurementUnitFamily? measurementUnitFamily =
            string.IsNullOrWhiteSpace(command.MeasurementUnitFamily)
                ? null
                : MeasurementUnitFamily.Create(command.MeasurementUnitFamily);

        AttributeDefinition attributeDefinition = null!;

        await _schemaCoordinator.ExecuteSchemaChangeAsync(
            productTypeId,
            async token =>
            {
                await _attributeDefinitionRegistry.EnsureKeyIsUniqueInHierarchyAsync(
                    productTypeId,
                    key,
                    token);

                attributeDefinition = productType.DefineAttribute(
                    key,
                    command.DataType,
                    command.Scope,
                    command.IsRequired,
                    command.DisplayOrder,
                    command.MinimumValue,
                    command.MaximumValue,
                    command.MinimumLength,
                    command.MaximumLength,
                    measurementUnitFamily);

                await _dbContext.SaveChangesAsync(token);
            },
            cancellationToken);

        return new DefineAttributeResult(attributeDefinition.Id.Value);
    }
}