using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.AddAttributeOption;

public sealed class AddAttributeOptionCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeSchemaCoordinator schemaCoordinator)
    : ICommandHandler<AddAttributeOptionCommand, AddAttributeOptionResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeSchemaCoordinator _schemaCoordinator =
        schemaCoordinator;

    public async ValueTask<AddAttributeOptionResult> Handle(
        AddAttributeOptionCommand command,
        CancellationToken cancellationToken)
    {
        ProductTypeId productTypeId = ProductTypeId.From(command.ProductTypeId);

        ProductType productType = await _dbContext.ProductTypes
            .Include(item => item.AttributeDefinitions)
            .ThenInclude(item => item.Options)
            .SingleOrDefaultAsync(
                item => item.Id == productTypeId,
                cancellationToken)
            ?? throw new ProductTypeDomainException(
                "product_type.not_found",
                $"Product type '{productTypeId}' was not found.");

        AttributeDefinitionId attributeDefinitionId =
            AttributeDefinitionId.From(command.AttributeDefinitionId);

        AttributeOptionCode optionCode =
            AttributeOptionCode.Create(command.Code);

        AttributeOption attributeOption = null!;

        await _schemaCoordinator.ExecuteSchemaChangeAsync(
            productTypeId,
            async token =>
            {
                attributeOption = productType.AddAttributeOption(
                    attributeDefinitionId,
                    optionCode,
                    command.DisplayOrder);

                await _dbContext.SaveChangesAsync(token);
            },
            cancellationToken);

        return new AddAttributeOptionResult(attributeOption.Id.Value);
    }
}