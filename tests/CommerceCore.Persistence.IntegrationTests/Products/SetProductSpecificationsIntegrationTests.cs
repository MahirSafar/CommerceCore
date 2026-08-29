using CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class SetProductSpecificationsIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WithValidSpecifications_PersistsJsonbAndSchemaVersion()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken,
            tenantId);

        IProductTypeEffectiveSchemaReader schemaReader =
            scope.ServiceProvider.GetRequiredService<
                IProductTypeEffectiveSchemaReader>();

        ICatalogSchemaValidator schemaValidator =
            scope.ServiceProvider.GetRequiredService<
                ICatalogSchemaValidator>();

        SetProductSpecificationsCommandHandler handler = new(
            dbContext,
            schemaReader,
            schemaValidator);

        AttributeValueBag specifications = AttributeValueBag.Empty.With(
            AttributeKey.Create("ram_gb"),
            AttributeValue.Integer.Create(16));

        SetProductSpecificationsResult result = await handler.Handle(
            new SetProductSpecificationsCommand(
                product.Id.Value,
                specifications),
            cancellationToken);

        Assert.True(result.Changed);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(42, result.ValidatedAgainstVersion);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.Equal(42, persistedProduct.ValidatedAgainstVersion);

        Assert.True(
            persistedProduct.Specifications.TryGetValue(
                AttributeKey.Create("ram_gb"),
                out AttributeValue? value));

        AttributeValue.Integer ram = Assert.IsType<AttributeValue.Integer>(
            value);

        Assert.Equal(16, ram.Value);
    }

    [Fact]
    public async Task Handle_WithMassMeasurement_NormalizesAndPersistsCanonicalValue()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken,
            tenantId,
            CreateMassMeasurementSchemaJson());

        IProductTypeEffectiveSchemaReader schemaReader =
            scope.ServiceProvider.GetRequiredService<
                IProductTypeEffectiveSchemaReader>();

        ICatalogSchemaValidator schemaValidator =
            scope.ServiceProvider.GetRequiredService<
                ICatalogSchemaValidator>();

        SetProductSpecificationsCommandHandler handler = new(
            dbContext,
            schemaReader,
            schemaValidator);

        ProductSpecificationsInput specifications = new(
            new Dictionary<AttributeKey, ProductSpecificationInputValue>
            {
                [AttributeKey.Create("weight")] =
                    new ProductSpecificationInputValue.Measurement(
                        1.5m,
                        "kg")
            });

        SetProductSpecificationsResult result = await handler.Handle(
            new SetProductSpecificationsCommand(
                product.Id.Value,
                specifications),
            cancellationToken);

        Assert.True(result.Changed);
        Assert.Equal(42, result.ValidatedAgainstVersion);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.True(
            persistedProduct.Specifications.TryGetValue(
                AttributeKey.Create("weight"),
                out AttributeValue? value));

        AttributeValue.Measurement weight = Assert.IsType<
            AttributeValue.Measurement>(value);

        Assert.Equal(1.5m, weight.Value);
        Assert.Equal("kg", weight.Unit);
        Assert.Equal(1500m, weight.CanonicalValue);
        Assert.Equal("g", weight.CanonicalUnit);
    }

    [Fact]
    public async Task Handle_WithUnitOutsideSchemaFamily_ThrowsValidationException()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken,
            tenantId,
            CreateMassMeasurementSchemaJson());

        IProductTypeEffectiveSchemaReader schemaReader =
            scope.ServiceProvider.GetRequiredService<
                IProductTypeEffectiveSchemaReader>();

        ICatalogSchemaValidator schemaValidator =
            scope.ServiceProvider.GetRequiredService<
                ICatalogSchemaValidator>();

        SetProductSpecificationsCommandHandler handler = new(
            dbContext,
            schemaReader,
            schemaValidator);

        ProductSpecificationsInput specifications = new(
            new Dictionary<AttributeKey, ProductSpecificationInputValue>
            {
                [AttributeKey.Create("weight")] =
                    new ProductSpecificationInputValue.Measurement(
                        15m,
                        "cm")
            });

        ValidationException exception = await Assert.ThrowsAsync<
            ValidationException>(() => handler.Handle(
                new SetProductSpecificationsCommand(
                    product.Id.Value,
                    specifications),
                cancellationToken).AsTask());

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "catalog_schema.measurement_unit_not_supported");

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.Empty(persistedProduct.Specifications.Values);
        Assert.Equal(0, persistedProduct.ValidatedAgainstVersion);
    }

    [Fact]
    public async Task Handle_WithInvalidSpecifications_ThrowsValidationException()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = fixture.SetTenantForCurrentTest();

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken,
            tenantId);

        IProductTypeEffectiveSchemaReader schemaReader =
            scope.ServiceProvider.GetRequiredService<
                IProductTypeEffectiveSchemaReader>();

        ICatalogSchemaValidator schemaValidator =
            scope.ServiceProvider.GetRequiredService<
                ICatalogSchemaValidator>();

        SetProductSpecificationsCommandHandler handler = new(
            dbContext,
            schemaReader,
            schemaValidator);

        ValidationException exception = await Assert.ThrowsAsync<
            ValidationException>(() => handler.Handle(
                new SetProductSpecificationsCommand(
                    product.Id.Value,
                    AttributeValueBag.Empty.With(
                        AttributeKey.Create("ram_gb"),
                        AttributeValue.Text.Create("sixteen"))),
                cancellationToken).AsTask());

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "catalog_schema.attribute_type_mismatch");

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.Empty(persistedProduct.Specifications.Values);
        Assert.Equal(0, persistedProduct.ValidatedAgainstVersion);
    }

    private static string CreateMassMeasurementSchemaJson() =>
    """
    {
      "attributes": [
        {
          "id": "00000000-0000-0000-0000-000000000002",
          "key": "weight",
          "scope": "ProductSpecification",
          "options": [],
          "dataType": "Measurement",
          "isRequired": false,
          "displayOrder": 0,
          "isDeprecated": false,
          "maximumValue": 10000,
          "minimumValue": 100,
          "maximumLength": null,
          "minimumLength": null,
          "enforcementStatus": "Enforced",
          "measurementUnitFamily": "mass"
        }
      ]
    }
    """;

    private async Task<Product> SeedProductAsync(
        CommerceCoreDbContext dbContext,
        CancellationToken cancellationToken,
        TenantId tenantId,
        string? schemaJson = null)
    {
        Guid productTypeId = Guid.NewGuid();

        string productTypeCode = $"spec_test_{Guid.NewGuid():N}";

        schemaJson ??=
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

        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Specification test product")
            ]);

        Product product = Product.Create(
            tenantId,
            name,
            Money.Create(100m, "USD"),
            ProductTypeId.From(productTypeId),
            new DateTimeOffset(
                2026, 8, 21, 10, 0, 0, TimeSpan.Zero));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }
}