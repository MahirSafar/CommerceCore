using CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Products;

[Collection(nameof(PostgreSqlCollection))]
public sealed class StorefrontProductReaderTests(PostgreSqlFixture fixture)
{
    [Fact]
    public async Task ReadAsync_IsolatesTenants_AndFiltersInvisibleProducts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TestTenantContext context = fixture.Services
            .GetRequiredService<TestTenantContext>();

        try
        {
            TenantId tenantA = await fixture.CreateTenantAsync(cancellationToken);
            TenantId tenantB = await fixture.CreateTenantAsync(cancellationToken);

            context.SetTenant(tenantA);
            SeedResult dataA = await SeedAsync(tenantA, cancellationToken);

            context.SetTenant(tenantB);
            SeedResult dataB = await SeedAsync(tenantB, cancellationToken);

            StorefrontProductPage pageB = await ReadAsync(
                new ListStorefrontProductsQuery(),
                cancellationToken);

            Assert.Equal(
                dataB.ActiveIds.Order(),
                pageB.Items.Select(item => item.ProductId).Order());

            context.SetTenant(tenantA);
            StorefrontProductPage pageA = await ReadAsync(
                new ListStorefrontProductsQuery(),
                cancellationToken);

            Assert.Equal(
                dataA.ActiveIds.Order(),
                pageA.Items.Select(item => item.ProductId).Order());
            Assert.Null(pageA.NextAfterProductId);
            Assert.All(pageA.Items, item =>
            {
                Assert.Equal("Test product", item.Name);
                Assert.Equal(10m, item.BasePriceAmount);
                Assert.Equal("AZN", item.Currency);
            });
        }
        finally
        {
            context.Clear();
        }
    }

    [Fact]
    public async Task ReadAsync_PaginatesWithinProductType_WithoutDuplicates()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        TestTenantContext context = fixture.Services
            .GetRequiredService<TestTenantContext>();

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
            context.SetTenant(tenantId);
            SeedResult data = await SeedAsync(tenantId, cancellationToken);

            var query = new ListStorefrontProductsQuery(
                PageSize: 2,
                ProductTypeId: data.ProductTypeId);

            StorefrontProductPage first = await ReadAsync(query, cancellationToken);
            Assert.Equal(2, first.Items.Count);
            Assert.NotNull(first.NextAfterProductId);

            StorefrontProductPage second = await ReadAsync(
                query with { AfterProductId = first.NextAfterProductId },
                cancellationToken);
            Assert.Single(second.Items);
            Assert.Null(second.NextAfterProductId);

            Guid[] actualIds = first.Items
                .Concat(second.Items)
                .Select(item => item.ProductId)
                .ToArray();

            Assert.Equal(3, actualIds.Distinct().Count());
            Assert.Equal(data.FilteredIds.Order(), actualIds.Order());
        }
        finally
        {
            context.Clear();
        }
    }

    [Fact]
    public async Task ReadAsync_WithoutTenant_RejectsTheRequest()
    {
        fixture.Services.GetRequiredService<TestTenantContext>().Clear();

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReadAsync(
            new ListStorefrontProductsQuery(),
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(101)]
    public void Validator_RejectsInvalidPageSize(int pageSize)
    {
        var validator = new ListStorefrontProductsQueryValidator();
        var result = validator.Validate(
            new ListStorefrontProductsQuery(PageSize: pageSize));

        Assert.False(result.IsValid);
    }

    private async Task<StorefrontProductPage> ReadAsync(
        ListStorefrontProductsQuery query,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var reader = scope.ServiceProvider
            .GetRequiredService<IStorefrontProductReader>();
        var handler = new ListStorefrontProductsQueryHandler(reader);

        StorefrontProductPage result = await handler.Handle(
            query,
            cancellationToken);

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();
        Assert.Empty(dbContext.ChangeTracker.Entries());

        return result;
    }

    private async Task<SeedResult> SeedAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..20]),
            isAssignable: true);

        var otherType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..20]),
            isAssignable: true);

        dbContext.ProductTypes.AddRange(productType, otherType);
        await dbContext.SaveChangesAsync(cancellationToken);

        Product[] activeProducts = Enumerable.Range(0, 3)
            .Select(_ => CreateProduct(tenantId, productType.Id))
            .ToArray();

        Product otherActive = CreateProduct(tenantId, otherType.Id);
        Product draft = CreateProduct(
            tenantId,
            productType.Id,
            activate: false);
        Product inactive = CreateProduct(tenantId, productType.Id);
        inactive.Deactivate();
        Product archived = CreateProduct(tenantId, productType.Id);
        archived.Archive(DateTimeOffset.UtcNow, "integration-test");

        dbContext.Products.AddRange(activeProducts);
        dbContext.Products.AddRange(
            otherActive,
            draft,
            inactive,
            archived);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new SeedResult(
            productType.Id.Value,
            activeProducts.Select(product => product.Id.Value).ToArray(),
            activeProducts
                .Select(product => product.Id.Value)
                .Append(otherActive.Id.Value)
                .ToArray());
    }

    private static Product CreateProduct(
        TenantId tenantId,
        ProductTypeId productTypeId,
        bool activate = true)
    {
        LanguageCode language = LanguageCode.Create("en");
        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Test product")
            ]);
        Money price = Money.Create(10m, "AZN");

        Product product = Product.Create(
            tenantId,
            name,
            price,
            productTypeId,
            DateTimeOffset.UtcNow);

        if (activate)
        {
            ProductVariant variant = product.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                price,
                AttributeValueBag.Empty,
                isDefault: true);

            product.ActivateVariant(variant.Id);
            product.Activate();
        }

        return product;
    }

    private sealed record SeedResult(
        Guid ProductTypeId,
        IReadOnlyList<Guid> FilteredIds,
        IReadOnlyList<Guid> ActiveIds);
}
