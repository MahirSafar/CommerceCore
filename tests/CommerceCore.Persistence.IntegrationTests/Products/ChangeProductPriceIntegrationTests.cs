using CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ChangeProductPriceIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenPriceChanges_UpdatesPriceAndAuditFields()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = CreateProduct();

        dbContext.Products.Add(product);
        await dbContext.SaveChangesAsync(cancellationToken);

        ChangeProductPriceCommandHandler handler = new ChangeProductPriceCommandHandler(dbContext);

        ChangeProductPriceResult? result = await handler.Handle(
            new ChangeProductPriceCommand(
                product.Id.Value,
                169.99m,
                "USD"),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal(169.99m, result.PriceAmount);
        Assert.Equal("USD", result.Currency);
        Assert.Equal("Draft", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products
            .SingleAsync(
                entity => entity.Id == product.Id,
                cancellationToken);

        Assert.Equal(169.99m, persistedProduct.Price.Amount);
        Assert.Equal("USD", persistedProduct.Price.Currency);
        Assert.NotNull(persistedProduct.UpdatedAtUtc);
        Assert.Equal("integration-test", persistedProduct.UpdatedBy);
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Price update test product")
            ]);

        return Product.Create(
            name,
            Money.Create(100m, "USD"),
            new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
    }
}