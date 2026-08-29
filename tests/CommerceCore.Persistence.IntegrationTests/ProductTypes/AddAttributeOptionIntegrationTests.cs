using System.Text.Json;
using CommerceCore.Application.Catalog.ProductTypes.Commands.AddAttributeOption;
using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.ProductTypes;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AddAttributeOptionIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_OnParent_RefreshesDescendantEffectiveSchema()
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

        CreateProductTypeResult rootResult = await createHandler.Handle(
            new CreateProductTypeCommand(
                $"clothing_{Guid.NewGuid():N}",
                ParentProductTypeId: null,
                IsAssignable: false),
            cancellationToken);

        CreateProductTypeResult childResult = await createHandler.Handle(
            new CreateProductTypeCommand(
                $"tshirt_{Guid.NewGuid():N}",
                rootResult.ProductTypeId,
                IsAssignable: true),
            cancellationToken);

        DefineAttributeCommandHandler defineAttributeHandler = new(
            dbContext,
            schemaCoordinator,
            attributeDefinitionRegistry);

        DefineAttributeResult attributeResult = await defineAttributeHandler.Handle(
            new DefineAttributeCommand(
                rootResult.ProductTypeId,
                Key: "color",
                DataType: AttributeDataType.SingleSelect,
                Scope: AttributeScope.VariantOption,
                IsRequired: true,
                DisplayOrder: 0,
                MinimumValue: null,
                MaximumValue: null,
                MinimumLength: null,
                MaximumLength: null,
                MeasurementUnitFamily: null),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        ProductType childBeforeOption = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(childResult.ProductTypeId),
            cancellationToken);

        long childPreviousSchemaVersion = childBeforeOption.OwnSchemaVersion;

        ProductTypeEffectiveSchema childEffectiveSchemaBefore = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                item => item.ProductTypeId == childBeforeOption.Id,
                cancellationToken);

        AddAttributeOptionCommandHandler addOptionHandler = new(
            dbContext,
            schemaCoordinator);

        AddAttributeOptionResult optionResult = await addOptionHandler.Handle(
            new AddAttributeOptionCommand(
                rootResult.ProductTypeId,
                attributeResult.AttributeDefinitionId,
                Code: "space-black",
                DisplayOrder: 0),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        ProductType childAfterOption = await dbContext.ProductTypes.SingleAsync(
            item => item.Id == ProductTypeId.From(childResult.ProductTypeId),
            cancellationToken);

        ProductTypeEffectiveSchema childEffectiveSchema = await dbContext
            .Set<ProductTypeEffectiveSchema>()
            .SingleAsync(
                item => item.ProductTypeId == childAfterOption.Id,
                cancellationToken);

        Assert.NotEqual(Guid.Empty, optionResult.AttributeOptionId);
        Assert.Equal(
            childPreviousSchemaVersion,
            childAfterOption.OwnSchemaVersion);

        Assert.True(childEffectiveSchema.EffectiveSchemaVersion > 0);
        Assert.True(
            childEffectiveSchema.EffectiveSchemaVersion >
            childEffectiveSchemaBefore.EffectiveSchemaVersion);

        using JsonDocument document = JsonDocument.Parse(childEffectiveSchema.Schema);

        JsonElement attribute = Assert.Single(
            document.RootElement
                .GetProperty("attributes")
                .EnumerateArray());

        Assert.Equal("color", attribute.GetProperty("key").GetString());
        Assert.Equal("SingleSelect", attribute.GetProperty("dataType").GetString());
        Assert.Equal("VariantOption", attribute.GetProperty("scope").GetString());

        JsonElement option = Assert.Single(
            attribute.GetProperty("options").EnumerateArray());

        Assert.Equal(
            optionResult.AttributeOptionId.ToString(),
            option.GetProperty("id").GetString());

        Assert.Equal("space-black", option.GetProperty("code").GetString());
        Assert.Equal(0, option.GetProperty("displayOrder").GetInt32());
        Assert.False(option.GetProperty("isDeprecated").GetBoolean());
    }
}