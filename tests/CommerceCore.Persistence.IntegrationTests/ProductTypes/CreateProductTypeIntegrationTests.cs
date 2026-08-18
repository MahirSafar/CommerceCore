using System.Text.Json;
using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.ProductTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class CreateProductTypeIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WithParent_CreatesProductTypeHierarchy()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeSchemaCoordinator schemaCoordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        CreateProductTypeCommandHandler handler = new(dbContext, schemaCoordinator);

        CreateProductTypeResult rootResult = await handler.Handle(
            new CreateProductTypeCommand(
                $"root_{Guid.NewGuid():N}",
                ParentProductTypeId: null,
                IsAssignable: false),
            cancellationToken);

        CreateProductTypeResult childResult = await handler.Handle(
            new CreateProductTypeCommand(
                $"child_{Guid.NewGuid():N}",
                ParentProductTypeId: rootResult.ProductTypeId,
                IsAssignable: true),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        ProductType child = await dbContext.ProductTypes
            .SingleAsync(
                productType =>
                    productType.Id ==
                    ProductTypeId.From(childResult.ProductTypeId),
                cancellationToken);

        Assert.Equal(
            ProductTypeId.From(rootResult.ProductTypeId),
            child.ParentProductTypeId);

        Assert.True(child.IsAssignable);
        Assert.Equal("integration-test", child.CreatedBy);

        var effectiveSchema = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                schema => schema.ProductTypeId == child.Id,
                cancellationToken);

        Assert.True(child.SchemaVersion > 0);
        Assert.Equal(child.SchemaVersion, effectiveSchema.SchemaVersion);

        using var document = JsonDocument.Parse(effectiveSchema.Schema);

        var attributes = document.RootElement.GetProperty("attributes");

        Assert.Equal(JsonValueKind.Array, attributes.ValueKind);
        Assert.Equal(0, attributes.GetArrayLength());
    }

    [Fact]
    public async Task Handle_WithUnknownParent_ThrowsDomainException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeSchemaCoordinator schemaCoordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        CreateProductTypeCommandHandler handler = new(dbContext, schemaCoordinator);

        ProductTypeDomainException exception = await Assert.ThrowsAsync<ProductTypeDomainException>(
            () => handler.Handle(
                new CreateProductTypeCommand(
                    $"orphan_{Guid.NewGuid():N}",
                    ProductTypeId.New().Value,
                    IsAssignable: true),
                cancellationToken).AsTask());

        Assert.Equal("product_type.parent_not_found", exception.Code);
    }
}