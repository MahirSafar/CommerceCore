using CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ActivateProductIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WithValidSpecifications_ActivatesAndRefreshesSchemaVersion()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            hasRamSpecification: true,
            cancellationToken);

        ActivateProductCommandHandler handler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        ActivateProductResult? result = await handler.Handle(
            new ActivateProductCommand(product.Id.Value),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal("Active", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.Equal(ProductStatus.Active, persistedProduct.Status);
        Assert.Equal(42, persistedProduct.ValidatedAgainstVersion);
    }

    [Fact]
    public async Task Handle_WhenRequiredSpecificationIsMissing_ThrowsValidationException()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            hasRamSpecification: false,
            cancellationToken);

        ActivateProductCommandHandler handler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        ValidationException exception = await Assert.ThrowsAsync<
            ValidationException>(() => handler.Handle(
                new ActivateProductCommand(product.Id.Value),
                cancellationToken).AsTask());

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "catalog_schema.required_attribute_missing");

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            item => item.Id == product.Id,
            cancellationToken);

        Assert.Equal(ProductStatus.Draft, persistedProduct.Status);
        Assert.Equal(0, persistedProduct.ValidatedAgainstVersion);
    }

    private static ActivateProductCommandHandler CreateHandler(
        IServiceProvider serviceProvider,
        CommerceCoreDbContext dbContext)
    {
        return new ActivateProductCommandHandler(
            dbContext,
            serviceProvider.GetRequiredService<
                IProductTypeEffectiveSchemaReader>(),
            serviceProvider.GetRequiredService<
                ICatalogSchemaValidator>());
    }

    private static async Task<Product> SeedProductAsync(
        CommerceCoreDbContext dbContext,
        bool hasRamSpecification,
        CancellationToken cancellationToken)
    {
        Guid productTypeId = Guid.NewGuid();

        string productTypeCode = $"activation_test_{Guid.NewGuid():N}";

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
                  "isRequired": true,
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
                code,
                is_assignable,
                own_schema_version,
                created_at_utc,
                created_by)
            VALUES (
                {productTypeId},
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
                effective_schema_version,
                schema,
                updated_at_utc)
            VALUES (
                {productTypeId},
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
                    "Activation test product")
            ]);

        Product product = Product.Create(
            name,
            Money.Create(100m, "USD"),
            ProductTypeId.From(productTypeId),
            new DateTimeOffset(
                2026, 8, 22, 10, 0, 0, TimeSpan.Zero));

        if (hasRamSpecification)
        {
            product.ApplyValidatedSpecifications(
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("ram_gb"),
                    AttributeValue.Integer.Create(16)),
                effectiveSchemaVersion: 1);
        }

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }
}