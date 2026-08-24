using CommerceCore.Application.Catalog.Products.Queries.GetProductVariantById;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class GetProductVariantByIdIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenVariantExists_ReturnsItsReadModel()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = CreateProduct();

        ProductVariant variant = product.AddVariant(
            VariantSku.Create($"laptop-black-{Guid.NewGuid():N}"),
            Money.Create(1299.99m, "USD"),
            Options("space-black"),
            isDefault: true);

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        GetProductVariantByIdQueryHandler handler = new(dbContext);

        GetProductVariantByIdResult? result = await handler.Handle(
            new GetProductVariantByIdQuery(
                product.Id.Value,
                variant.Id.Value),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(variant.Id.Value, result.ProductVariantId);
        Assert.Equal(variant.Sku.Value, result.Sku);
        Assert.Equal(1299.99m, result.PriceAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("Draft", result.Status);
        Assert.True(result.IsDefault);

        Assert.Equal(
            "space-black",
            Assert.IsType<AttributeValue.SingleSelect>(
                result.Options.Values[
                    AttributeKey.Create("color")]).OptionCode);
    }

    [Fact]
    public async Task Handle_WhenVariantDoesNotBelongToProduct_ReturnsNull()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await using AsyncServiceScope scope =
            fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = CreateProduct();

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        GetProductVariantByIdQueryHandler handler = new(dbContext);

        GetProductVariantByIdResult? result = await handler.Handle(
            new GetProductVariantByIdQuery(
                product.Id.Value,
                ProductVariantId.New().Value),
            cancellationToken);

        Assert.Null(result);
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    $"Variant query test {Guid.NewGuid():N}")
            ]);

        return Product.Create(
            name,
            Money.Create(1000m, "USD"),
            SeededCatalogIds.LegacyUnclassifiedProductTypeId,
            new DateTimeOffset(
                2026, 8, 24, 17, 0, 0, TimeSpan.Zero));
    }

    private static AttributeValueBag Options(string color) =>
        AttributeValueBag.Empty.With(
            AttributeKey.Create("color"),
            AttributeValue.SingleSelect.Create(color));
}