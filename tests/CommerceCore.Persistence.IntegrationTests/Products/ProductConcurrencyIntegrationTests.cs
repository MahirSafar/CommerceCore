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

        await using var scope1 = fixture.Services.CreateAsyncScope();
        CommerceCoreDbContext dbContext1 = scope1.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        await using var scope2 = fixture.Services.CreateAsyncScope();
        CommerceCoreDbContext dbContext2 = scope2.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product1 = await dbContext1.Products
            .SingleAsync(
                item => item.Id == ProductId.From(productId),
                cancellationToken);

        Product product2 = await dbContext2.Products
            .SingleAsync(
                item => item.Id == ProductId.From(productId),
                cancellationToken);

        product1.ChangePrice(Money.Create(120m, "USD"));
        product2.ChangePrice(Money.Create(130m, "USD"));

        await dbContext1.SaveChangesAsync(cancellationToken);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => dbContext2.SaveChangesAsync(cancellationToken));
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
            SeededCatalogIds.LegacyUnclassifiedProductTypeId,
            new DateTimeOffset(2026, 8, 15, 20, 0, 0, TimeSpan.Zero));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product.Id.Value;
    }
}