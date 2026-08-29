using CommerceCore.Application.Catalog.Products.Queries.GetProductById;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class GetProductByIdIntegrationTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenProductExists_ReturnsItsReadModel()
    {
        var cancellationToken = TestContext.Current.CancellationToken;

        TenantId tenantId = TenantId.New();
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..12]),
            isAssignable: true);

        dbContext.ProductTypes.Add(productType);
        await dbContext.SaveChangesAsync(cancellationToken);

        var product = CreateProduct(tenantId, productType.Id);

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

        TenantId tenantId = TenantId.New();
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..12]),
            isAssignable: true);

        dbContext.ProductTypes.Add(productType);
        await dbContext.SaveChangesAsync(cancellationToken);

        var product = CreateProduct(tenantId, productType.Id);

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

    private static Product CreateProduct(TenantId tenantId, ProductTypeId productTypeId)
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
            tenantId,
            name,
            Money.Create(149.99m, "USD"),
            productTypeId,
            new DateTimeOffset(2026, 8, 15, 17, 0, 0, TimeSpan.Zero));
    }
}
