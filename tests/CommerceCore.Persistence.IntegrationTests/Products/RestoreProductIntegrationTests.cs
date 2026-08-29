using CommerceCore.Application.Catalog.Products.Commands.RestoreProduct;
using CommerceCore.Application.Catalog.Products.Queries.GetProductById;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class RestoreProductIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task Handle_WhenProductIsArchived_RestoresItAndMakesItQueryable()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TenantId tenantId = TenantId.New();
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        Guid productId = await SeedArchivedActiveProductAsync(tenantId, cancellationToken);

        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        RestoreProductCommandHandler restoreHandler = new RestoreProductCommandHandler(dbContext);

        RestoreProductResult? result = await restoreHandler.Handle(
            new RestoreProductCommand(productId),
            cancellationToken);

        Assert.NotNull(result);
        Assert.True(result.Restored);
        Assert.Equal("Inactive", result.Status);

        dbContext.ChangeTracker.Clear();

        GetProductByIdQueryHandler getHandler = new GetProductByIdQueryHandler(dbContext);

        GetProductByIdResult? restoredProduct = await getHandler.Handle(
            new GetProductByIdQuery(productId),
            cancellationToken);

        Assert.NotNull(restoredProduct);
        Assert.Equal("Inactive", restoredProduct.Status);
    }

    private async Task<Guid> SeedArchivedActiveProductAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..12]),
            isAssignable: true);

        dbContext.ProductTypes.Add(productType);
        await dbContext.SaveChangesAsync(cancellationToken);

        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Archived product")
            ]);

        Product product = Product.Create(
            tenantId,
            name,
            Money.Create(79.99m, "USD"),
            productType.Id,
            new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero));

        ProductVariant variant = product.AddVariant(
            VariantSku.Create($"restore-variant-{Guid.NewGuid():N}"),
            Money.Create(79.99m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        product.ActivateVariant(variant.Id);

        product.Activate();

        product.Archive(
            new DateTimeOffset(2026, 8, 16, 10, 5, 0, TimeSpan.Zero),
            "integration-test");

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product.Id.Value;
    }
}