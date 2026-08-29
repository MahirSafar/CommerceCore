using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttributeKeyHierarchyIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenKeyExistsOnAncestor_ThrowsDomainException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeSchemaCoordinator schemaCoordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        IAttributeDefinitionRegistry attributeDefinitionRegistry = scope.ServiceProvider
            .GetRequiredService<IAttributeDefinitionRegistry>();

        ITenantContext tenantContext = scope.ServiceProvider
            .GetRequiredService<ITenantContext>();

        CreateProductTypeCommandHandler createHandler = new(
            dbContext,
            schemaCoordinator,
            tenantContext);

        DefineAttributeCommandHandler defineHandler = new(
            dbContext,
            schemaCoordinator,
            attributeDefinitionRegistry);

        (Guid rootId, Guid childId) = await CreateHierarchyAsync(
            createHandler,
            cancellationToken);

        await defineHandler.Handle(
            CreateRamGbCommand(rootId),
            cancellationToken);

        ProductTypeDomainException exception =
            await Assert.ThrowsAsync<ProductTypeDomainException>(
                () => defineHandler.Handle(
                    CreateRamGbCommand(childId),
                    cancellationToken).AsTask());

        Assert.Equal(
            "product_type.attribute_key_exists_in_hierarchy",
            exception.Code);
    }

    [Fact]
    public async Task Handle_WhenKeyExistsOnDescendant_ThrowsDomainException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeSchemaCoordinator schemaCoordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        IAttributeDefinitionRegistry attributeDefinitionRegistry = scope.ServiceProvider
            .GetRequiredService<IAttributeDefinitionRegistry>();

        ITenantContext tenantContext = scope.ServiceProvider
            .GetRequiredService<ITenantContext>();

        CreateProductTypeCommandHandler createHandler = new(
            dbContext,
            schemaCoordinator,
            tenantContext);

        DefineAttributeCommandHandler defineHandler = new(
            dbContext,
            schemaCoordinator,
            attributeDefinitionRegistry);

        (Guid rootId, Guid childId) = await CreateHierarchyAsync(
            createHandler,
            cancellationToken);

        await defineHandler.Handle(
            CreateRamGbCommand(childId),
            cancellationToken);

        ProductTypeDomainException exception =
            await Assert.ThrowsAsync<ProductTypeDomainException>(
                () => defineHandler.Handle(
                    CreateRamGbCommand(rootId),
                    cancellationToken).AsTask());

        Assert.Equal(
            "product_type.attribute_key_exists_in_hierarchy",
            exception.Code);
    }

    private static async Task<(Guid RootId, Guid ChildId)> CreateHierarchyAsync(
        CreateProductTypeCommandHandler createHandler,
        CancellationToken cancellationToken)
    {
        CreateProductTypeResult root = await createHandler.Handle(
            new CreateProductTypeCommand(
                $"root_{Guid.NewGuid():N}",
                ParentProductTypeId: null,
                IsAssignable: false),
            cancellationToken);

        CreateProductTypeResult child = await createHandler.Handle(
            new CreateProductTypeCommand(
                $"child_{Guid.NewGuid():N}",
                root.ProductTypeId,
                IsAssignable: true),
            cancellationToken);

        return (root.ProductTypeId, child.ProductTypeId);
    }

    private static DefineAttributeCommand CreateRamGbCommand(
        Guid productTypeId) =>
        new(
            productTypeId,
            Key: "ram_gb",
            DataType: AttributeDataType.Integer,
            Scope: AttributeScope.ProductSpecification,
            IsRequired: false,
            DisplayOrder: 0,
            MinimumValue: 4,
            MaximumValue: 256,
            MinimumLength: null,
            MaximumLength: null,
            MeasurementUnitFamily: null);
}