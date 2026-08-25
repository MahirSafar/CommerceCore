using CommerceCore.Application.Catalog.Products.Commands.ActivateProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.DeactivateProductVariant;
using CommerceCore.Application.Catalog.Products.Commands.SetProductDefaultVariant;
using CommerceCore.Application.Catalog.Products.Queries.GetProductVariants;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Exceptions;
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

    [Fact]
    public async Task Handle_WithAnotherVariant_MakesItTheOnlyDefaultVariant()
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

        AddProductVariantCommandHandler addHandler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        AddProductVariantResult first = await addHandler.Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-black-{Guid.NewGuid():N}",
                1299.99m,
                "USD",
                Options("space-black"),
                IsDefault: true),
            cancellationToken);

        AddProductVariantResult second = await addHandler.Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-silver-{Guid.NewGuid():N}",
                1349.99m,
                "USD",
                Options("silver"),
                IsDefault: false),
            cancellationToken);

        SetProductDefaultVariantResult? result =
            await CreateSetDefaultHandler(dbContext).Handle(
                new SetProductDefaultVariantCommand(
                    product.Id.Value,
                    second.ProductVariantId),
                cancellationToken);

        Assert.NotNull(result);
        Assert.True(result.DefaultChanged);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(second.ProductVariantId, result.ProductVariantId);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        ProductVariant firstVariant = persistedProduct.Variants.Single(
            item => item.Id.Value == first.ProductVariantId);

        ProductVariant secondVariant = persistedProduct.Variants.Single(
            item => item.Id.Value == second.ProductVariantId);

        Assert.False(firstVariant.IsDefault);
        Assert.True(secondVariant.IsDefault);
        Assert.Single(persistedProduct.Variants, item => item.IsDefault);
    }

    [Fact]
    public async Task Handle_ReturnsProductVariants_WithDefaultVariantFirst()
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

        AddProductVariantCommandHandler addHandler = CreateHandler(
            scope.ServiceProvider,
            dbContext);

        AddProductVariantResult defaultVariant =
            await addHandler.Handle(
                new AddProductVariantCommand(
                    product.Id.Value,
                    $"laptop-black-{Guid.NewGuid():N}",
                    1299.99m,
                    "USD",
                    Options("space-black"),
                    IsDefault: true),
                cancellationToken);

        AddProductVariantResult secondaryVariant =
            await addHandler.Handle(
                new AddProductVariantCommand(
                    product.Id.Value,
                    $"laptop-silver-{Guid.NewGuid():N}",
                    1349.99m,
                    "USD",
                    Options("silver"),
                    IsDefault: false),
                cancellationToken);

        GetProductVariantsResult? result = await CreateListHandler(
            dbContext).Handle(
            new GetProductVariantsQuery(product.Id.Value),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(2, result.Variants.Count);

        GetProductVariantListItem first = result.Variants[0];
        GetProductVariantListItem second = result.Variants[1];

        Assert.Equal(defaultVariant.ProductVariantId, first.ProductVariantId);
        Assert.True(first.IsDefault);
        Assert.Equal("Draft", first.Status);
        Assert.Equal("space-black", GetColor(first.Options));

        Assert.Equal(secondaryVariant.ProductVariantId, second.ProductVariantId);
        Assert.False(second.IsDefault);
        Assert.Equal("Draft", second.Status);
        Assert.Equal("silver", GetColor(second.Options));
    }

    [Fact]
    public async Task Handle_WithValidDraftVariant_ActivatesAndPersistsStatus()
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

        AddProductVariantResult added = await CreateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-black-{Guid.NewGuid():N}",
                1299.99m,
                "USD",
                Options("space-black"),
                IsDefault: true),
            cancellationToken);

        ActivateProductVariantResult? result = await CreateActivateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new ActivateProductVariantCommand(
                product.Id.Value,
                added.ProductVariantId),
            cancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Activated);
        Assert.Equal("Active", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        ProductVariant variant = Assert.Single(persistedProduct.Variants);

        Assert.Equal(ProductVariantStatus.Active, variant.Status);
    }

    [Fact]
    public async Task Handle_WhenParentIsNotActive_DeactivatesActiveVariant()
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

        AddProductVariantResult added = await CreateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-black-{Guid.NewGuid():N}",
                1299.99m,
                "USD",
                Options("space-black"),
                IsDefault: true),
            cancellationToken);

        await CreateActivateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new ActivateProductVariantCommand(
                product.Id.Value,
                added.ProductVariantId),
            cancellationToken);

        DeactivateProductVariantResult? result =
            await CreateDeactivateHandler(dbContext).Handle(
                new DeactivateProductVariantCommand(
                    product.Id.Value,
                    added.ProductVariantId),
                cancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Deactivated);
        Assert.Equal("Inactive", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        Assert.Equal(ProductStatus.Draft, persistedProduct.Status);
        Assert.Equal(
            ProductVariantStatus.Inactive,
            Assert.Single(persistedProduct.Variants).Status);
    }

    [Fact]
    public async Task Handle_WhenVariantIsLastActiveOfActiveProduct_ThrowsDomainException()
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

        AddProductVariantResult added = await CreateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new AddProductVariantCommand(
                product.Id.Value,
                $"laptop-black-{Guid.NewGuid():N}",
                1299.99m,
                "USD",
                Options("space-black"),
                IsDefault: true),
            cancellationToken);

        await CreateActivateHandler(
            scope.ServiceProvider,
            dbContext).Handle(
            new ActivateProductVariantCommand(
                product.Id.Value,
                added.ProductVariantId),
            cancellationToken);

        dbContext.ChangeTracker.Clear();

        Product activeProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        activeProduct.Activate();

        await dbContext.SaveChangesAsync(cancellationToken);

        ProductDomainException exception = await Assert.ThrowsAsync<
            ProductDomainException>(() =>
                CreateDeactivateHandler(dbContext).Handle(
                    new DeactivateProductVariantCommand(
                        product.Id.Value,
                        added.ProductVariantId),
                    cancellationToken).AsTask());

        Assert.Equal(
            "product.last_active_variant_cannot_be_deactivated",
            exception.Code);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        Assert.Equal(ProductStatus.Active, persistedProduct.Status);
        Assert.Equal(
            ProductVariantStatus.Active,
            Assert.Single(persistedProduct.Variants).Status);
    }



    private static AddProductVariantCommandHandler CreateHandler(
        IServiceProvider services,
        CommerceCoreDbContext dbContext) =>
        new(
            dbContext,
            services.GetRequiredService<
                IProductTypeEffectiveSchemaReader>(),
            services.GetRequiredService<ICatalogSchemaValidator>());

    private static ActivateProductVariantCommandHandler
    CreateActivateHandler(
        IServiceProvider services,
        CommerceCoreDbContext dbContext) =>
    new(
        dbContext,
        services.GetRequiredService<
            IProductTypeEffectiveSchemaReader>(),
        services.GetRequiredService<ICatalogSchemaValidator>());

    private static DeactivateProductVariantCommandHandler
        CreateDeactivateHandler(
            CommerceCoreDbContext dbContext) =>
        new(dbContext);

    private static SetProductDefaultVariantCommandHandler
    CreateSetDefaultHandler(
        CommerceCoreDbContext dbContext) =>
    new(dbContext);

    private static GetProductVariantsQueryHandler CreateListHandler(
        CommerceCoreDbContext dbContext) =>
        new(dbContext);

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
                    },
                    {
                      "id": "00000000-0000-0000-0000-000000000012",
                      "code": "silver",
                      "displayOrder": 1,
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

    private static string GetColor(AttributeValueBag options)
    {
        Assert.True(
            options.TryGetValue(
                AttributeKey.Create("color"),
                out AttributeValue? value));

        return Assert.IsType<AttributeValue.SingleSelect>(
            value).OptionCode;
    }
}