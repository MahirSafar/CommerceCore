using CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
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
public sealed class AddProductVariantIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WithValidVariantOptions_PersistsVariant()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken);

        AddProductVariantCommandHandler handler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        AddProductVariantResult result = await handler.Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-black-{Guid.NewGuid():N}",
                1299.99m,
                "USD",
                Options("space-black"),
                IsDefault: true),
            cancellationToken);

        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal("Draft", result.Status);
        Assert.True(result.IsDefault);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        ProductVariant variant = Assert.Single(persistedProduct.Variants);

        Assert.Equal(result.ProductVariantId, variant.Id.Value);
        Assert.Equal(result.Sku, variant.Sku.Value);
        Assert.Equal(1299.99m, variant.Price.Amount);
        Assert.Equal("USD", variant.Price.Currency);
        Assert.True(variant.IsDefault);
        Assert.Equal("space-black", GetColor(variant));
    }

    [Fact]
    public async Task Handle_WhenRequiredVariantOptionIsMissing_ThrowsValidationException()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await SeedProductAsync(
            dbContext,
            cancellationToken);

        AddProductVariantCommandHandler handler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        ValidationException exception = await Assert.ThrowsAsync<
            ValidationException>(() => handler.Handle(
                new AddProductVariantCommand(
                    product.Id.Value,
                    $"laptop-no-color-{Guid.NewGuid():N}",
                    1299.99m,
                    "USD",
                    AttributeValueBag.Empty,
                    IsDefault: true),
                cancellationToken).AsTask());

        Assert.Contains(
            exception.Errors,
            error => error.ErrorCode ==
                "catalog_schema.required_attribute_missing");

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        Assert.Empty(persistedProduct.Variants);
    }

    private static AddProductVariantCommandHandler CreateHandler(
        IServiceProvider services,
        CommerceCoreDbContext dbContext) =>
        new(
            dbContext,
            services.GetRequiredService<
                IProductTypeEffectiveSchemaReader>(),
            services.GetRequiredService<ICatalogSchemaValidator>());

    private static async Task<Product> SeedProductAsync(
        CommerceCoreDbContext dbContext,
        CancellationToken cancellationToken)
    {
        Guid productTypeId = Guid.NewGuid();

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
                {$"variant_test_{Guid.NewGuid():N}"},
                TRUE,
                0,
                CURRENT_TIMESTAMP,
                'integration-test');
            """,
            cancellationToken);

        string schemaJson =
            """
            {
              "attributes": [
                {
                  "id": "00000000-0000-0000-0000-000000000010",
                  "key": "color",
                  "scope": "VariantOption",
                  "options": [
                    {
                      "id": "00000000-0000-0000-0000-000000000011",
                      "code": "space-black",
                      "displayOrder": 0,
                      "isDeprecated": false
                    }
                  ],
                  "dataType": "SingleSelect",
                  "isRequired": true,
                  "displayOrder": 0,
                  "isDeprecated": false,
                  "maximumValue": null,
                  "minimumValue": null,
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
            INSERT INTO catalog.product_type_effective_schema (
                product_type_id,
                effective_schema_version,
                schema,
                updated_at_utc)
            VALUES (
                {productTypeId},
                1,
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
                    "Variant command test product")
            ]);

        Product product = Product.Create(
            name,
            Money.Create(1000m, "USD"),
            ProductTypeId.From(productTypeId),
            new DateTimeOffset(
                2026, 8, 24, 15, 0, 0, TimeSpan.Zero));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product;
    }

    private static AttributeValueBag Options(string color) =>
        AttributeValueBag.Empty.With(
            AttributeKey.Create("color"),
            AttributeValue.SingleSelect.Create(color));

    private static string GetColor(ProductVariant variant)
    {
        Assert.True(
            variant.Options.TryGetValue(
                AttributeKey.Create("color"),
                out AttributeValue? value));

        return Assert.IsType<AttributeValue.SingleSelect>(
            value).OptionCode;
    }
}