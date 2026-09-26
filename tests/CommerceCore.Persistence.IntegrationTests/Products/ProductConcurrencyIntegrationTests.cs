using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
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
public sealed class ProductConcurrencyIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChanges_WhenProductWasChangedConcurrently_ThrowsConcurrencyException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        Guid productId = await SeedProductAsync(tenantId, cancellationToken);

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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenActivationRacesWithDefaultVariantDeactivation_PreservesInvariant(
        bool activationCommitsFirst)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var tenantContext = fixture.Services
            .GetRequiredService<TestTenantContext>();

        TenantId? previousTenant = tenantContext.TenantId;

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(
                cancellationToken);

            tenantContext.SetTenant(tenantId);

            Guid productId = await SeedProductAsync(
                tenantId,
                cancellationToken);

            ProductId typedProductId = ProductId.From(productId);

            // Start with a draft product containing one active default variant.
            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                Product product = await seedDb.Products
                    .Include(item => item.Variants)
                    .SingleAsync(
                        item => item.Id == typedProductId,
                        cancellationToken);

                ProductVariant variant = product.AddVariant(
                    VariantSku.Create($"race_{Guid.NewGuid():N}"),
                    Money.Create(99.99m, "USD"),
                    AttributeValueBag.Empty,
                    isDefault: true);

                Assert.True(product.ActivateVariant(variant.Id));
                Assert.Equal(ProductStatus.Draft, product.Status);

                await seedDb.SaveChangesAsync(cancellationToken);
            }

            await using var activationScope =
                fixture.Services.CreateAsyncScope();

            await using var deactivationScope =
                fixture.Services.CreateAsyncScope();

            var activationDb = activationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var deactivationDb = deactivationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            // Keep two separate database connections open.
            await activationDb.Database.OpenConnectionAsync(cancellationToken);
            await deactivationDb.Database.OpenConnectionAsync(cancellationToken);

            // Both readers observe the same state before either writer commits.
            Product activationProduct = await activationDb.Products
                .Include(item => item.Variants)
                .SingleAsync(
                    item => item.Id == typedProductId,
                    cancellationToken);

            Product deactivationProduct = await deactivationDb.Products
                .Include(item => item.Variants)
                .SingleAsync(
                    item => item.Id == typedProductId,
                    cancellationToken);

            Assert.Equal(ProductStatus.Draft, activationProduct.Status);
            Assert.Equal(ProductStatus.Draft, deactivationProduct.Status);

            ProductVariant defaultVariant = Assert.Single(
                deactivationProduct.Variants);

            Assert.True(defaultVariant.IsDefault);
            Assert.Equal(ProductVariantStatus.Active, defaultVariant.Status);

            Assert.True(activationProduct.Activate());
            Assert.True(
                deactivationProduct.DeactivateVariant(defaultVariant.Id));

            CommerceCoreDbContext firstWriter = activationCommitsFirst
                ? activationDb
                : deactivationDb;

            CommerceCoreDbContext secondWriter = activationCommitsFirst
                ? deactivationDb
                : activationDb;

            await firstWriter.SaveChangesAsync(cancellationToken);

            Exception? secondWriteException = await Record.ExceptionAsync(
                async () =>
                {
                    await secondWriter.SaveChangesAsync(cancellationToken);
                });

            // Verify committed state using a fresh context.
            await using var verificationScope =
                fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            Product persisted = await verificationDb.Products
                .AsNoTracking()
                .Include(item => item.Variants)
                .SingleAsync(
                    item => item.Id == typedProductId,
                    cancellationToken);

            Assert.True(
                persisted.Status != ProductStatus.Active ||
                persisted.Variants.Any(
                    variant => variant.IsDefault &&
                               variant.Status == ProductVariantStatus.Active),
                "An active product was persisted without an active default variant.");

            Assert.IsType<DbUpdateConcurrencyException>(secondWriteException);

            Assert.Equal(
                activationCommitsFirst ? ProductStatus.Active : ProductStatus.Draft,
                persisted.Status);

            ProductVariant persistedDefault = Assert.Single(persisted.Variants);

            Assert.True(persistedDefault.IsDefault);
            Assert.Equal(
                activationCommitsFirst
                    ? ProductVariantStatus.Active
                    : ProductVariantStatus.Inactive,
                persistedDefault.Status);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenArchiveRacesWithVariantPriceChange_RejectsStaleWrite(
        bool archiveCommitsFirst)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var tenantContext = fixture.Services
            .GetRequiredService<TestTenantContext>();

        TenantId? previousTenant = tenantContext.TenantId;

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
            tenantContext.SetTenant(tenantId);

            ProductId productId = await SeedProductWithActiveDefaultVariantAsync(
                tenantId,
                cancellationToken);

            await using var archiveScope = fixture.Services.CreateAsyncScope();
            await using var priceScope = fixture.Services.CreateAsyncScope();

            var archiveDb = archiveScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var priceDb = priceScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            await archiveDb.Database.OpenConnectionAsync(cancellationToken);
            await priceDb.Database.OpenConnectionAsync(cancellationToken);

            Product archiveProduct = await archiveDb.Products
                .Include(product => product.Variants)
                .SingleAsync(product => product.Id == productId, cancellationToken);

            Product priceProduct = await priceDb.Products
                .Include(product => product.Variants)
                .SingleAsync(product => product.Id == productId, cancellationToken);

            Assert.True(archiveProduct.Archive(
                new DateTimeOffset(2026, 9, 25, 12, 0, 0, TimeSpan.Zero),
                "concurrency-test"));

            Guid archiveEventId = Assert.Single(
                archiveProduct.DomainEvents).EventId;

            ProductVariant variant = Assert.Single(priceProduct.Variants);
            Assert.True(variant.ChangePrice(Money.Create(125m, "USD")));

            CommerceCoreDbContext firstWriter = archiveCommitsFirst
                ? archiveDb
                : priceDb;

            CommerceCoreDbContext secondWriter = archiveCommitsFirst
                ? priceDb
                : archiveDb;

            await firstWriter.SaveChangesAsync(cancellationToken);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => secondWriter.SaveChangesAsync(cancellationToken));

            await using var verificationScope = fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            // Ignore the soft-delete filter to inspect an archived product.
            // PostgreSQL tenant RLS remains in effect.
            Product persisted = await verificationDb.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Include(product => product.Variants)
                .SingleAsync(product => product.Id == productId, cancellationToken);

            Assert.Equal(archiveCommitsFirst, persisted.IsDeleted);

            ProductVariant persistedVariant = Assert.Single(persisted.Variants);

            Assert.Equal(
                archiveCommitsFirst ? 99.99m : 125m,
                persistedVariant.Price.Amount);

            Assert.Equal(ProductVariantStatus.Active, persistedVariant.Status);
            Assert.True(persistedVariant.IsDefault);

            bool archiveEventPersisted = await verificationDb.OutboxMessages
                .AnyAsync(message => message.Id == archiveEventId, cancellationToken);

            Assert.Equal(archiveCommitsFirst, archiveEventPersisted);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenVariantOperationsAreNoOps_DoesNotUpdateProduct(
        bool useAsyncSave)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var tenantContext = fixture.Services
            .GetRequiredService<TestTenantContext>();

        TenantId? previousTenant = tenantContext.TenantId;

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
            tenantContext.SetTenant(tenantId);

            ProductId productId = await SeedProductWithActiveDefaultVariantAsync(
                tenantId,
                cancellationToken);

            await using var scope = fixture.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            Product product = await db.Products
                .Include(item => item.Variants)
                .SingleAsync(item => item.Id == productId, cancellationToken);

            uint originalXmin = db.Entry(product)
                .Property<uint>("xmin")
                .CurrentValue;

            var originalUpdatedAtUtc = product.UpdatedAtUtc;
            var originalUpdatedBy = product.UpdatedBy;

            ProductVariant variant = Assert.Single(product.Variants);

            Assert.False(product.ActivateVariant(variant.Id));
            Assert.False(product.SetDefaultVariant(variant.Id));
            Assert.False(variant.ChangePrice(Money.Create(99.99m, "USD")));

            int affected = useAsyncSave
                ? await db.SaveChangesAsync(cancellationToken)
                : db.SaveChanges();

            Assert.Equal(0, affected);

            db.ChangeTracker.Clear();

            Product persisted = await db.Products
                .SingleAsync(item => item.Id == productId, cancellationToken);

            uint persistedXmin = db.Entry(persisted)
                .Property<uint>("xmin")
                .CurrentValue;

            Assert.Equal(originalXmin, persistedXmin);
            Assert.Equal(originalUpdatedAtUtc, persisted.UpdatedAtUtc);
            Assert.Equal(originalUpdatedBy, persisted.UpdatedBy);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SaveChanges_WhenDefaultSwitchRacesWithTargetDeactivation_PreservesInvariant(
        bool defaultSwitchCommitsFirst)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var tenantContext = fixture.Services
            .GetRequiredService<TestTenantContext>();

        TenantId? previousTenant = tenantContext.TenantId;

        try
        {
            TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
            tenantContext.SetTenant(tenantId);

            ProductId productId = await SeedProductWithActiveDefaultVariantAsync(
                tenantId,
                cancellationToken);

            ProductVariantId originalDefaultId;
            ProductVariantId targetVariantId;

            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                Product product = await seedDb.Products
                    .Include(item => item.Variants)
                    .SingleAsync(item => item.Id == productId, cancellationToken);

                originalDefaultId = Assert.Single(product.Variants).Id;

                ProductVariant target = product.AddVariant(
                    VariantSku.Create($"default_race_{Guid.NewGuid():N}"),
                    Money.Create(99.99m, "USD"),
                    AttributeValueBag.Empty.With(
                        AttributeKey.Create("color"),
                        AttributeValue.SingleSelect.Create("blue")),
                    isDefault: false);

                targetVariantId = target.Id;

                Assert.True(product.ActivateVariant(targetVariantId));
                Assert.True(product.Activate());

                await seedDb.SaveChangesAsync(cancellationToken);
            }

            await using var switchScope = fixture.Services.CreateAsyncScope();
            await using var deactivateScope = fixture.Services.CreateAsyncScope();

            var switchDb = switchScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var deactivateDb = deactivateScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            await switchDb.Database.OpenConnectionAsync(cancellationToken);
            await deactivateDb.Database.OpenConnectionAsync(cancellationToken);

            Product switchProduct = await switchDb.Products
                .Include(item => item.Variants)
                .SingleAsync(item => item.Id == productId, cancellationToken);

            Product deactivateProduct = await deactivateDb.Products
                .Include(item => item.Variants)
                .SingleAsync(item => item.Id == productId, cancellationToken);

            Assert.Equal(ProductStatus.Active, switchProduct.Status);
            Assert.Equal(ProductStatus.Active, deactivateProduct.Status);

            Assert.Equal(
                originalDefaultId,
                Assert.Single(
                    switchProduct.Variants,
                    variant => variant.IsDefault).Id);

            Assert.Equal(
                originalDefaultId,
                Assert.Single(
                    deactivateProduct.Variants,
                    variant => variant.IsDefault).Id);

            Assert.True(switchProduct.SetDefaultVariant(targetVariantId));
            Assert.True(deactivateProduct.DeactivateVariant(targetVariantId));

            CommerceCoreDbContext firstWriter = defaultSwitchCommitsFirst
                ? switchDb
                : deactivateDb;

            CommerceCoreDbContext secondWriter = defaultSwitchCommitsFirst
                ? deactivateDb
                : switchDb;

            await firstWriter.SaveChangesAsync(cancellationToken);

            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
                () => secondWriter.SaveChangesAsync(cancellationToken));

            await using var verificationScope = fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            Product persisted = await verificationDb.Products
                .AsNoTracking()
                .Include(item => item.Variants)
                .SingleAsync(item => item.Id == productId, cancellationToken);

            Assert.Equal(ProductStatus.Active, persisted.Status);
            Assert.Equal(2, persisted.Variants.Count);

            ProductVariant persistedDefault = Assert.Single(
                persisted.Variants,
                variant => variant.IsDefault);

            Assert.Equal(ProductVariantStatus.Active, persistedDefault.Status);

            Assert.Equal(
                defaultSwitchCommitsFirst ? targetVariantId : originalDefaultId,
                persistedDefault.Id);

            ProductVariant persistedTarget = Assert.Single(
                persisted.Variants,
                variant => variant.Id == targetVariantId);

            Assert.Equal(
                defaultSwitchCommitsFirst
                    ? ProductVariantStatus.Active
                    : ProductVariantStatus.Inactive,
                persistedTarget.Status);

            ProductVariant persistedOriginal = Assert.Single(
                persisted.Variants,
                variant => variant.Id == originalDefaultId);

            Assert.Equal(ProductVariantStatus.Active, persistedOriginal.Status);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    private async Task<ProductId> SeedProductWithActiveDefaultVariantAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        Guid productId = await SeedProductAsync(tenantId, cancellationToken);
        ProductId typedProductId = ProductId.From(productId);

        await using var scope = fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        Product product = await db.Products
            .Include(item => item.Variants)
            .SingleAsync(item => item.Id == typedProductId, cancellationToken);

        ProductVariant variant = product.AddVariant(
            VariantSku.Create($"guard_{Guid.NewGuid():N}"),
            Money.Create(99.99m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        Assert.True(product.ActivateVariant(variant.Id));

        await db.SaveChangesAsync(cancellationToken);

        return typedProductId;
    }

    private async Task<Guid> SeedProductAsync(
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

        LanguageCode defaultLanguage = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            defaultLanguage,
            [
                new KeyValuePair<LanguageCode, string>(
                    defaultLanguage,
                    "Concurrency test product")
            ]);

        Product product = Product.Create(
            tenantId,
            name,
            Money.Create(99.99m, "USD"),
            productType.Id,
            new DateTimeOffset(2026, 8, 15, 20, 0, 0, TimeSpan.Zero));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        return product.Id.Value;
    }
}