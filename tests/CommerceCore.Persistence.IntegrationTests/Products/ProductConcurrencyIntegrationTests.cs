using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductConcurrencyIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChanges_WhenProductWasChangedConcurrently_ThrowsConcurrencyException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Guid productId = await SeedProductAsync(cancellationToken);

        await using var firstScope = fixture.Services.CreateAsyncScope();
        await using var secondScope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext firstDbContext = firstScope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        CommerceCoreDbContext secondDbContext = secondScope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        ProductId typedProductId = ProductId.From(productId);

        Product firstProduct = await firstDbContext.Products
            .SingleAsync(product => product.Id == typedProductId, cancellationToken);

        Product secondProduct = await secondDbContext.Products
            .SingleAsync(product => product.Id == typedProductId, cancellationToken);

        Assert.True(firstProduct.Activate());
        Assert.True(secondProduct.Activate());

        await firstDbContext.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            async () => await secondDbContext.SaveChangesAsync(
                cancellationToken));
    }

    private async Task<Guid> SeedProductAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        LanguageCode defaultLanguage = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            defaultLanguage,
            [
                new KeyValuePair<LanguageCode, string>(
                    defaultLanguage,
                    "Concurrency test product")
            ]);

        Product product = Product.Create(
            name,
            Money.Create(99.99m, "USD"),
            new DateTimeOffset(2026, 8, 15, 20, 0, 0, TimeSpan.Zero));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product.Id.Value;
    }
}