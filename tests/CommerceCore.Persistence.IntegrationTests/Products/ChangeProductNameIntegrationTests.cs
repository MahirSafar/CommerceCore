using CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;
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
public sealed class ChangeProductNameIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenNameChanges_UpdatesJsonbNameAndAuditFields()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

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

        ChangeProductNameCommandHandler handler = new(dbContext);

        ChangeProductNameResult? result = await handler.Handle(
            new ChangeProductNameCommand(
                product.Id.Value,
                "az",
                new Dictionary<string, string>
                {
                    ["az"] = "Yeni məhsul adı",
                    ["en"] = "New product name"
                }),
            cancellationToken);

        Assert.NotNull(result);
        Assert.Equal(product.Id.Value, result.ProductId);
        Assert.Equal("az", result.DefaultLanguage);
        Assert.Equal("Yeni məhsul adı", result.NameTranslations["az"]);
        Assert.Equal("New product name", result.NameTranslations["en"]);
        Assert.Equal("Draft", result.Status);

        dbContext.ChangeTracker.Clear();

        Product persistedProduct = await dbContext.Products.SingleAsync(
            entity => entity.Id == product.Id,
            cancellationToken);

        Assert.Equal("az", persistedProduct.Name.DefaultLanguage.Value);
        Assert.Equal(
            "Yeni məhsul adı",
            persistedProduct.Name.Get(LanguageCode.Create("az")));
        Assert.Equal(
            "New product name",
            persistedProduct.Name.Get(LanguageCode.Create("en")));
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
                    "Original product name")
            ]);

        return Product.Create(
            tenantId,
            name,
            Money.Create(100m, "USD"),
            productTypeId,
            new DateTimeOffset(2026, 8, 16, 9, 0, 0, TimeSpan.Zero));
    }
}