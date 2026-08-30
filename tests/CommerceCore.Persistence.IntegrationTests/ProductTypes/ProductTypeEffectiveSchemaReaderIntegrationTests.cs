using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductTypeEffectiveSchemaReaderIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task GetAsync_WhenEffectiveSchemaExists_MapsTypedSchema()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeEffectiveSchemaReader reader = scope.ServiceProvider
            .GetRequiredService<IProductTypeEffectiveSchemaReader>();

        Guid productTypeId = Guid.NewGuid();

        string productTypeCode = $"reader_{Guid.NewGuid():N}";

        string schemaJson =
            """
            {
              "attributes": [
                {
                  "id": "00000000-0000-0000-0000-000000000001",
                  "key": "ram_gb",
                  "scope": "ProductSpecification",
                  "options": [],
                  "dataType": "Integer",
                  "isRequired": false,
                  "displayOrder": 0,
                  "isDeprecated": false,
                  "maximumValue": 256,
                  "minimumValue": 4,
                  "maximumLength": null,
                  "minimumLength": null,
                  "enforcementStatus": "Enforced",
                  "measurementUnitFamily": null
                },
                {
                  "id": "00000000-0000-0000-0000-000000000002",
                  "key": "color",
                  "scope": "VariantOption",
                  "options": [
                    {
                      "id": "00000000-0000-0000-0000-000000000003",
                      "code": "space-black",
                      "displayOrder": 0,
                      "isDeprecated": false
                    }
                  ],
                  "dataType": "SingleSelect",
                  "isRequired": true,
                  "displayOrder": 1,
                  "isDeprecated": false,
                  "maximumValue": null,
                  "minimumValue": null,
                  "maximumLength": null,
                  "minimumLength": null,
                  "enforcementStatus": "Draft",
                  "measurementUnitFamily": null
                }
              ]
            }
            """;

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO catalog.product_types (
                id,
                tenant_id,
                code,
                is_assignable,
                own_schema_version,
                created_at_utc,
                created_by)
            VALUES (
                {productTypeId},
                {tenantId.Value},
                {productTypeCode},
                TRUE,
                0,
                CURRENT_TIMESTAMP,
                'integration-test');
            """,
            cancellationToken);

        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO catalog.product_type_effective_schema (
                product_type_id,
                tenant_id,
                effective_schema_version,
                schema,
                updated_at_utc)
            VALUES (
                {productTypeId},
                {tenantId.Value},
                42,
                CAST({schemaJson} AS jsonb),
                CURRENT_TIMESTAMP);
            """,
            cancellationToken);

        EffectiveProductTypeSchema? schema = await reader.GetAsync(
            ProductTypeId.From(productTypeId),
            cancellationToken);

        Assert.NotNull(schema);
        Assert.Equal(42, schema.EffectiveSchemaVersion);
        Assert.Equal(2, schema.Attributes.Count);

        EffectiveAttributeDefinition ram = schema.Attributes.Single(
            attribute => attribute.Key == AttributeKey.Create("ram_gb"));

        Assert.Equal(AttributeDataType.Integer, ram.DataType);
        Assert.Equal(AttributeScope.ProductSpecification, ram.Scope);
        Assert.False(ram.IsRequired);
        Assert.Equal(AttributeEnforcementStatus.Enforced, ram.EnforcementStatus);
        Assert.Equal(4m, ram.MinimumValue);
        Assert.Equal(256m, ram.MaximumValue);
        Assert.Empty(ram.Options);

        EffectiveAttributeDefinition color = schema.Attributes.Single(
            attribute => attribute.Key == AttributeKey.Create("color"));

        Assert.Equal(AttributeDataType.SingleSelect, color.DataType);
        Assert.Equal(AttributeScope.VariantOption, color.Scope);
        Assert.True(color.IsRequired);
        Assert.Equal(AttributeEnforcementStatus.Draft, color.EnforcementStatus);

        EffectiveAttributeOption option = Assert.Single(color.Options);

        Assert.Equal("space-black", option.Code.Value);
        Assert.False(option.IsDeprecated);
    }

    [Fact]
    public async Task GetAsync_WhenEffectiveSchemaDoesNotExist_ReturnsNull()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        IProductTypeEffectiveSchemaReader reader = scope.ServiceProvider
            .GetRequiredService<IProductTypeEffectiveSchemaReader>();

        EffectiveProductTypeSchema? schema = await reader.GetAsync(
            ProductTypeId.New(),
            cancellationToken);

        Assert.Null(schema);
    }
}