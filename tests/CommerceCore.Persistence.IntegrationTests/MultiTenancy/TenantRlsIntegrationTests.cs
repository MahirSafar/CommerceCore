using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

[Collection(nameof(PostgreSqlCollection))]
public sealed class TenantRlsIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public TenantRlsIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    private static LocalizedText CreateName(string text) =>
        LocalizedText.Create(LanguageCode.Create("en"), new Dictionary<LanguageCode, string> { { LanguageCode.Create("en"), text } });

    private static Money CreatePrice(decimal amount) =>
        Money.Create(amount, "USD");

    [Fact]
    public async Task Rls_TenantA_Creates_Product_Successfully()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange
        TenantId tenantA = _fixture.PrimaryTenantId;
        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantA);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(tenantA, ProductTypeCode.Create("electronics_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
        db.ProductTypes.Add(productType);
        await db.SaveChangesAsync(cancellationToken);

        // Act
        var product = Product.Create(tenantA, CreateName("Laptop A"), CreatePrice(1200), productType.Id, DateTimeOffset.UtcNow);
        product.AddVariant(VariantSku.Create("LAPTOP-A-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(1200), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);

        // Assert
        var savedProduct = await db.Products.FirstOrDefaultAsync(
            p => p.Id == product.Id,
            cancellationToken);
        Assert.NotNull(savedProduct);
        Assert.Equal(tenantA, savedProduct.TenantId);
    }

    [Fact]
    public async Task Rls_TenantB_Cannot_Get_TenantA_Product()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange: Create Product in Tenant A
        TenantId tenantA = _fixture.PrimaryTenantId;
        TenantId tenantB = _fixture.SecondaryTenantId;

        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantA);

        ProductId productAId;
        {
            await using var scopeA = _fixture.Services.CreateAsyncScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productType = ProductType.CreateRoot(tenantA, ProductTypeCode.Create("type_a_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
            dbA.ProductTypes.Add(productType);
            await dbA.SaveChangesAsync(cancellationToken);

            var product = Product.Create(tenantA, CreateName("Product of Tenant A"), CreatePrice(100), productType.Id, DateTimeOffset.UtcNow);
            product.AddVariant(VariantSku.Create("SKU-A-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(100), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
            dbA.Products.Add(product);
            await dbA.SaveChangesAsync(cancellationToken);
            productAId = product.Id;
        }

        // Act: Tenant B queries the product
        tenantContext.SetTenant(tenantB);
        {
            await using var scopeB = _fixture.Services.CreateAsyncScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productUnderTenantB = await dbB.Products.FirstOrDefaultAsync(
                p => p.Id == productAId,
                cancellationToken);

            // Assert
            Assert.Null(productUnderTenantB);
        }
    }

    [Fact]
    public async Task Rls_TenantB_Cannot_Update_Or_Delete_TenantA_Product()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange
        TenantId tenantA = _fixture.PrimaryTenantId;
        TenantId tenantB = _fixture.SecondaryTenantId;

        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantA);

        ProductId productAId;
        {
            await using var scopeA = _fixture.Services.CreateAsyncScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productType = ProductType.CreateRoot(tenantA, ProductTypeCode.Create("update_a_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
            dbA.ProductTypes.Add(productType);
            await dbA.SaveChangesAsync(cancellationToken);

            var product = Product.Create(tenantA, CreateName("Initial Name"), CreatePrice(50), productType.Id, DateTimeOffset.UtcNow);
            product.AddVariant(VariantSku.Create("SKU-UPD-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(50), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
            dbA.Products.Add(product);
            await dbA.SaveChangesAsync(cancellationToken);
            productAId = product.Id;
        }

        // Act & Assert under Tenant B
        tenantContext.SetTenant(tenantB);
        {
            await using var scopeB = _fixture.Services.CreateAsyncScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            // Attempting to find or update
            var productToUpdate = await dbB.Products.FirstOrDefaultAsync(
                p => p.Id == productAId,
                cancellationToken);
            Assert.Null(productToUpdate);

            // Raw SQL update under Tenant B session should affect 0 rows due to RLS
            int affected = await dbB.Database.ExecuteSqlRawAsync(
                "UPDATE catalog.products SET created_by = 'hacked' WHERE id = {0}",
                [productAId.Value],
                cancellationToken);

            Assert.Equal(0, affected);
        }

        // Verify product remains unchanged in Tenant A
        tenantContext.SetTenant(tenantA);
        {
            await using var scopeA = _fixture.Services.CreateAsyncScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var originalProduct = await dbA.Products.FirstOrDefaultAsync(
                p => p.Id == productAId,
                cancellationToken);
            Assert.NotNull(originalProduct);
            Assert.Equal("Initial Name", originalProduct.Name.ToString());
        }
    }

    [Fact]
    public async Task Rls_Without_Tenant_Context_Returns_No_Products()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange: Create Product in Tenant A
        TenantId tenantA = _fixture.PrimaryTenantId;
        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantA);

        {
            await using var scopeA = _fixture.Services.CreateAsyncScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productType = ProductType.CreateRoot(tenantA, ProductTypeCode.Create("no_ctx_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
            dbA.ProductTypes.Add(productType);
            await dbA.SaveChangesAsync(cancellationToken);

            var product = Product.Create(tenantA, CreateName("Tenant A Product"), CreatePrice(200), productType.Id, DateTimeOffset.UtcNow);
            product.AddVariant(VariantSku.Create("SKU-NOCTX-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(200), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
            dbA.Products.Add(product);
            await dbA.SaveChangesAsync(cancellationToken);
        }

        // Act: Clear context (no tenant)
        tenantContext.Clear();
        {
            await using var scopeNoCtx = _fixture.Services.CreateAsyncScope();
            var dbNoCtx = scopeNoCtx.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var products = await dbNoCtx.Products.ToListAsync(cancellationToken);

            // Assert: RLS NULLIF returns NULL, no product rows should be visible
            Assert.Empty(products);
        }
    }

    [Fact]
    public async Task Rls_TenantA_Cannot_Link_Product_To_TenantB_ProductType()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange
        TenantId tenantA = _fixture.PrimaryTenantId;
        TenantId tenantB = _fixture.SecondaryTenantId;

        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();

        // 1. Create ProductType under Tenant B
        tenantContext.SetTenant(tenantB);
        ProductTypeId typeBId;
        {
            await using var scopeB = _fixture.Services.CreateAsyncScope();
            var dbB = scopeB.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productTypeB = ProductType.CreateRoot(tenantB, ProductTypeCode.Create("cross_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
            dbB.ProductTypes.Add(productTypeB);
            await dbB.SaveChangesAsync(cancellationToken);
            typeBId = productTypeB.Id;
        }

        // 2. Tenant A attempts to create Product referencing Tenant B's ProductType
        tenantContext.SetTenant(tenantA);
        {
            await using var scopeA = _fixture.Services.CreateAsyncScope();
            var dbA = scopeA.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            var productA = Product.Create(tenantA, CreateName("Illegal Cross-Tenant Product"), CreatePrice(300), typeBId, DateTimeOffset.UtcNow);
            productA.AddVariant(VariantSku.Create("SKU-CROSS-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(300), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
            dbA.Products.Add(productA);

            // Assert: Should fail DB composite Foreign Key constraint
            await Assert.ThrowsAnyAsync<DbUpdateException>(async () =>
            {
                await dbA.SaveChangesAsync(cancellationToken);
            });
        }
    }

    [Fact]
    public async Task Rls_Outbox_Message_Retains_TenantId()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // Arrange
        TenantId tenantA = _fixture.PrimaryTenantId;
        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantA);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(tenantA, ProductTypeCode.Create("outbox_" + Guid.NewGuid().ToString("N")[..8]), isAssignable: true);
        db.ProductTypes.Add(productType);
        await db.SaveChangesAsync(cancellationToken);

        var product = Product.Create(tenantA, CreateName("Outbox Product"), CreatePrice(450), productType.Id, DateTimeOffset.UtcNow);
        product.AddVariant(VariantSku.Create("SKU-OUTBOX-" + Guid.NewGuid().ToString("N")[..6]), CreatePrice(450), CommerceCore.Domain.Catalog.Attributes.ValueObjects.AttributeValueBag.Empty, isDefault: true);
        db.Products.Add(product);

        // Act: Save creates domain events and writes to Outbox
        await db.SaveChangesAsync(cancellationToken);

        // Assert: Outbox messages must have TenantId of Tenant A
        var outboxMessage = await db.OutboxMessages.FirstOrDefaultAsync(
            m => m.TenantId == tenantA,
            cancellationToken);
        Assert.NotNull(outboxMessage);
        Assert.Equal(tenantA, outboxMessage.TenantId);
    }

    [Fact]
    public async Task Rls_TenantA_Cannot_Link_ProductType_To_TenantB_Parent()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantA = _fixture.PrimaryTenantId;
        TenantId tenantB = _fixture.SecondaryTenantId;

        var tenantContext = _fixture.Services
            .GetRequiredService<TestTenantContext>();

        tenantContext.SetTenant(tenantB);

        ProductTypeId tenantBRootId;

        await using (var scopeB = _fixture.Services.CreateAsyncScope())
        {
            var dbB = scopeB.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var tenantBRoot = ProductType.CreateRoot(
                tenantB,
                ProductTypeCode.Create(
                    $"tenant_b_root_{Guid.NewGuid():N}"));

            dbB.ProductTypes.Add(tenantBRoot);

            await dbB.SaveChangesAsync(cancellationToken);

            tenantBRootId = tenantBRoot.Id;
        }

        tenantContext.SetTenant(tenantA);

        await using (var scopeA = _fixture.Services.CreateAsyncScope())
        {
            var dbA = scopeA.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var illegalChild = ProductType.CreateChild(
                tenantA,
                tenantBRootId,
                ProductTypeCode.Create(
                    $"tenant_a_child_{Guid.NewGuid():N}"));

            dbA.ProductTypes.Add(illegalChild);

            await Assert.ThrowsAnyAsync<DbUpdateException>(
                () => dbA.SaveChangesAsync(cancellationToken));
        }
    }

    [Fact]
    public async Task AppRole_Cannot_Modify_Platform_Data()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        _fixture.SetTenantForCurrentTest();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
            () => db.Database.ExecuteSqlRawAsync(
                "UPDATE platform.storefronts SET is_active = is_active",
                cancellationToken));

        Assert.Equal(PostgresErrorCodes.InsufficientPrivilege, exception.SqlState);
    }
}
