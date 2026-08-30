using System.Text.Json;
using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.ProductTypes;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class DefineAttributeIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_OnParent_UpdatesEffectiveSchemaForEntireSubtree()
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

        CreateProductTypeCommandHandler createProductTypeHandler = new(
            dbContext,
            schemaCoordinator,
            tenantContext);

        CreateProductTypeResult rootResult = await createProductTypeHandler.Handle(
            new CreateProductTypeCommand(
                $"electronics_{Guid.NewGuid():N}",
                ParentProductTypeId: null,
                IsAssignable: false),
            cancellationToken);

        CreateProductTypeResult childResult = await createProductTypeHandler.Handle(
            new CreateProductTypeCommand(
                $"laptop_{Guid.NewGuid():N}",
                rootResult.ProductTypeId,
                IsAssignable: true),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        ProductType rootBeforeChange = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(rootResult.ProductTypeId),
            cancellationToken);

        ProductType childBeforeChange = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(childResult.ProductTypeId),
            cancellationToken);

        long rootPreviousVersion = rootBeforeChange.OwnSchemaVersion;
        long childPreviousVersion = childBeforeChange.OwnSchemaVersion;

        ProductTypeEffectiveSchema childEffectiveSchemaBefore = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                item => item.ProductTypeId == childBeforeChange.Id,
                cancellationToken);

        DefineAttributeCommandHandler defineAttributeHandler = new(
            dbContext,
            schemaCoordinator,
            attributeDefinitionRegistry);

        DefineAttributeResult result = await defineAttributeHandler.Handle(
            new DefineAttributeCommand(
                rootResult.ProductTypeId,
                Key: "ram_gb",
                DataType: AttributeDataType.Integer,
                Scope: AttributeScope.ProductSpecification,
                IsRequired: false,
                DisplayOrder: 0,
                MinimumValue: 4,
                MaximumValue: 256,
                MinimumLength: null,
                MaximumLength: null,
                MeasurementUnitFamily: null),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        ProductType rootAfterChange = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(rootResult.ProductTypeId),
            cancellationToken);

        ProductType childAfterChange = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(childResult.ProductTypeId),
            cancellationToken);

        ProductTypeEffectiveSchema childEffectiveSchema = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                item => item.ProductTypeId == childAfterChange.Id,
                cancellationToken);

        Assert.NotEqual(Guid.Empty, result.AttributeDefinitionId);

        Assert.True(rootAfterChange.OwnSchemaVersion > rootPreviousVersion);
        Assert.Equal(
            rootPreviousVersion + 1,
            rootAfterChange.OwnSchemaVersion);

        Assert.Equal(
            childPreviousVersion,
            childAfterChange.OwnSchemaVersion);

        Assert.True(childEffectiveSchema.EffectiveSchemaVersion > 0);
        Assert.True(
            childEffectiveSchema.EffectiveSchemaVersion >
            childEffectiveSchemaBefore.EffectiveSchemaVersion);

        ProductTypeEffectiveSchema rootEffectiveSchema = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                item => item.ProductTypeId == rootAfterChange.Id,
                cancellationToken);

        Assert.Equal(
            rootEffectiveSchema.EffectiveSchemaVersion,
            childEffectiveSchema.EffectiveSchemaVersion);

        using JsonDocument document = JsonDocument.Parse(childEffectiveSchema.Schema);

        JsonElement attributes = document.RootElement.GetProperty("attributes");

        Assert.Equal(JsonValueKind.Array, attributes.ValueKind);

        JsonElement attribute = Assert.Single(attributes.EnumerateArray());

        Assert.Equal(result.AttributeDefinitionId.ToString(), attribute.GetProperty("id").GetString());
        Assert.Equal("ram_gb", attribute.GetProperty("key").GetString());
        Assert.Equal("Integer", attribute.GetProperty("dataType").GetString());
        Assert.Equal(
            "ProductSpecification",
            attribute.GetProperty("scope").GetString());

        Assert.False(attribute.GetProperty("isRequired").GetBoolean());
        Assert.Equal(0, attribute.GetProperty("displayOrder").GetInt32());
        Assert.Equal(4, attribute.GetProperty("minimumValue").GetDecimal());
        Assert.Equal(256, attribute.GetProperty("maximumValue").GetDecimal());
    }
}