using CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
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

        TenantId tenantId = TenantId.New();
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        ProductType productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..12]),
            isAssignable: true);

        dbContext.ProductTypes.Add(productType);
        await dbContext.SaveChangesAsync(cancellationToken);

        Product product = CreateProduct(tenantId, productType.Id);

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

    private static Product CreateProduct(TenantId tenantId, ProductTypeId productTypeId)
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
            tenantId,
            name,
            Money.Create(100m, "USD"),
            productTypeId,
            new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
    }
}