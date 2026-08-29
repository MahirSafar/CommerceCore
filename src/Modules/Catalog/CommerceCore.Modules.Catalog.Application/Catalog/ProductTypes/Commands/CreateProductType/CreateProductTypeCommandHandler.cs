using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;

public sealed class CreateProductTypeCommandHandler(
    ICommerceCoreDbContext dbContext,
    IProductTypeSchemaCoordinator schemaCoordinator)
    : ICommandHandler<CreateProductTypeCommand, CreateProductTypeResult>
{
    private readonly ICommerceCoreDbContext _dbContext = dbContext;
    private readonly IProductTypeSchemaCoordinator _schemaCoordinator =
        schemaCoordinator;

    public async ValueTask<CreateProductTypeResult> Handle(
        CreateProductTypeCommand command,
        CancellationToken cancellationToken)
    {
        ProductTypeCode code = ProductTypeCode.Create(command.Code);

        bool codeAlreadyExists = await _dbContext.ProductTypes
            .AnyAsync(
                productType => productType.Code == code,
                cancellationToken);

        if (codeAlreadyExists)
        {
            throw new ProductTypeDomainException(
                "product_type.code_already_exists",
                $"Product type code '{code}' already exists.");
        }

        ProductType productType;

        if (command.ParentProductTypeId is Guid parentProductTypeGuid)
        {
            ProductTypeId parentProductTypeId = ProductTypeId.From(
                parentProductTypeGuid);

            bool parentExists = await _dbContext.ProductTypes
                .AnyAsync(
                    productType => productType.Id == parentProductTypeId,
                    cancellationToken);

            if (!parentExists)
            {
                throw new ProductTypeDomainException(
                    "product_type.parent_not_found",
                    $"Parent product type '{parentProductTypeId}' was not found.");
            }

            productType = ProductType.CreateChild(
                parentProductTypeId,
                code,
                command.IsAssignable);
        }
        else
        {
            productType = ProductType.CreateRoot(
                code,
                command.IsAssignable);
        }

        await _schemaCoordinator.ExecuteCreationAsync(
            productType.Id,
            productType.ParentProductTypeId,
            async token =>
            {
                _dbContext.ProductTypes.Add(productType);

                await _dbContext.SaveChangesAsync(token);
            },
            cancellationToken);

        return new CreateProductTypeResult(productType.Id.Value);
    }
}