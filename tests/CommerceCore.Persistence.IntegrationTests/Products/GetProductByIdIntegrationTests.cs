using CommerceCore.Application.Catalog.Products.Queries.GetProductById;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class GetProductByIdIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenProductExists_ReturnsItsReadModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var product = CreateProduct();

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        var handler = new GetProductByIdQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetProductByIdQuery(product.Id.Value),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(product.ProductTypeId.Value, result.ProductTypeId);
        Assert.Equal("az", result.DefaultLanguage);
        Assert.Equal("Sınaq məhsulu", result.NameTranslations["az"]);
        Assert.Equal("Test product", result.NameTranslations["en"]);
        Assert.Equal(149.99m, result.PriceAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("Draft", result.Status);
    }

    [Fact]
    public async Task Handle_WhenProductIsArchived_ReturnsNull()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var product = CreateProduct();

        Assert.True(product.Archive(
            new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero),
            "integration-test"));

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        var handler = new GetProductByIdQueryHandler(dbContext);

        var result = await handler.Handle(
            new GetProductByIdQuery(product.Id.Value),
            cancellationToken);

        Assert.Null(result);
    }

    private static Product CreateProduct()
    {
        var defaultLanguage = LanguageCode.Create("az");

        var name = LocalizedText.Create(
            defaultLanguage,
            [
                new KeyValuePair<LanguageCode, string>(
                    defaultLanguage,
                    "Sınaq məhsulu"),
                new KeyValuePair<LanguageCode, string>(
                    LanguageCode.Create("en"),
                    "Test product")
            ]);

        return Product.Create(
            name,
            Money.Create(149.99m, "USD"),
            SeededCatalogIds.LegacyUnclassifiedProductTypeId,
            new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero));
    }
}
