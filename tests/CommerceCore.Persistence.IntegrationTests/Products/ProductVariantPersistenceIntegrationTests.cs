using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductVariantPersistenceIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChanges_WithVariants_PersistsAndLoadsVariantCollection()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = CreateProduct();

        ProductVariant black = product.AddVariant(
            VariantSku.Create($"laptop-black-{Guid.NewGuid():N}"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductVariant silver = product.AddVariant(
            VariantSku.Create($"laptop-silver-{Guid.NewGuid():N}"),
            Money.Create(1250m, "USD"),
            Options("silver"),
            isDefault: false);

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .Include(item => item.Variants)
            .SingleAsync(
                item => item.Id == product.Id,
                cancellationToken);

        Assert.Equal(2, persistedProduct.Variants.Count);

        ProductVariant persistedBlack = persistedProduct.Variants
            .Single(item => item.Id == black.Id);

        Assert.Equal(black.Sku, persistedBlack.Sku);
        Assert.Equal(1200m, persistedBlack.Price.Amount);
        Assert.Equal("USD", persistedBlack.Price.Currency);
        Assert.True(persistedBlack.IsDefault);
        Assert.Equal(ProductVariantStatus.Draft, persistedBlack.Status);
        Assert.Equal("space-black", GetColor(persistedBlack));

        ProductVariant persistedSilver = persistedProduct.Variants
            .Single(item => item.Id == silver.Id);

        Assert.Equal(silver.Sku, persistedSilver.Sku);
        Assert.Equal(1250m, persistedSilver.Price.Amount);
        Assert.Equal("USD", persistedSilver.Price.Currency);
        Assert.False(persistedSilver.IsDefault);
        Assert.Equal(ProductVariantStatus.Draft, persistedSilver.Status);
        Assert.Equal("silver", GetColor(persistedSilver));
    }

    [Fact]
    public async Task SaveChanges_WithDuplicateGlobalSku_ThrowsDbUpdateException()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        VariantSku sharedSku = VariantSku.Create(
            $"global-sku-{Guid.NewGuid():N}");

        Product firstProduct = CreateProduct();
        firstProduct.AddVariant(
            sharedSku,
            Money.Create(100m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        Product secondProduct = CreateProduct();
        secondProduct.AddVariant(
            sharedSku,
            Money.Create(200m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        dbContext.Products.AddRange(firstProduct, secondProduct);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => dbContext.SaveChangesAsync(cancellationToken));
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    $"Variant persistence product {Guid.NewGuid():N}")
            ]);

        return Product.Create(
            name,
            Money.Create(1000m, "USD"),
            SeededCatalogIds.LegacyUnclassifiedProductTypeId,
            new DateTimeOffset(
                2026, 8, 24, 12, 0, 0, TimeSpan.Zero));
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