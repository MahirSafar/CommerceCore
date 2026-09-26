using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductTypeSchemaRetryIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteSchemaChange_AfterSaveFailure_PersistsExactlyOneAttribute(
        bool injectTransientFailure)
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

            ProductTypeId productTypeId;

            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                ProductType productType = ProductType.CreateRoot(
                    tenantId,
                    ProductTypeCode.Create($"retry_{Guid.NewGuid():N}"),
                    isAssignable: true);

                seedDb.ProductTypes.Add(productType);
                await seedDb.SaveChangesAsync(cancellationToken);

                productTypeId = productType.Id;
            }

            await using var operationScope =
                fixture.Services.CreateAsyncScope();

            var operationDb = operationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var coordinator = operationScope.ServiceProvider
                .GetRequiredService<IProductTypeSchemaCoordinator>();

            int attempts = 0;

            Exception? failure = await Record.ExceptionAsync(
                () => coordinator.ExecuteSchemaChangeAsync(
                    productTypeId,
                    async token =>
                    {
                        attempts++;

                        ProductType productType = await operationDb.ProductTypes
                            .Include(item => item.AttributeDefinitions)
                            .SingleAsync(
                                item => item.Id == productTypeId,
                                token);

                        productType.DefineAttribute(
                            AttributeKey.Create("retry_attribute"),
                            AttributeDataType.Text,
                            AttributeScope.ProductSpecification,
                            isRequired: false,
                            displayOrder: 0);

                        await operationDb.SaveChangesAsync(token);

                        // The database write completed inside the transaction.
                        // Simulate one transient failure before outer commit.
                        if (injectTransientFailure && attempts == 1)
                        {
                            var simulatedFailure = new NpgsqlException(
                                "Injected failure after SaveChanges.",
                                new TimeoutException(
                                    "Simulated transient timeout."));

                            Assert.True(simulatedFailure.IsTransient);

                            throw simulatedFailure;
                        }
                    },
                    cancellationToken));

            Assert.Equal(injectTransientFailure ? 2 : 1, attempts);
            Assert.Null(failure);

            // Read committed state through a separate context.
            await using var verificationScope =
                fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            ProductType persisted = await verificationDb.ProductTypes
                .AsNoTracking()
                .Include(item => item.AttributeDefinitions)
                .SingleAsync(
                    item => item.Id == productTypeId,
                    cancellationToken);

            AttributeDefinition definition = Assert.Single(
                persisted.AttributeDefinitions);

            Assert.Equal("retry_attribute", definition.Key.Value);
            Assert.Equal(tenantId, definition.TenantId);
            Assert.Equal(1L, persisted.OwnSchemaVersion);

            var schemaReader = verificationScope.ServiceProvider
                .GetRequiredService<IProductTypeEffectiveSchemaReader>();

            var schema = await schemaReader.GetAsync(
                productTypeId,
                cancellationToken);

            Assert.NotNull(schema);
            Assert.True(schema.EffectiveSchemaVersion > 0);

            var effectiveAttribute = Assert.Single(schema.Attributes);

            Assert.Equal("retry_attribute", effectiveAttribute.Key.Value);
            Assert.Equal(AttributeDataType.Text, effectiveAttribute.DataType);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteCreation_AfterSaveFailure_PersistsExactlyOneProductType(
        bool injectTransientFailure)
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

            ProductTypeCode code = ProductTypeCode.Create(
                $"retry_creation_{Guid.NewGuid():N}");

            ProductType productType = ProductType.CreateRoot(
                tenantId,
                code,
                isAssignable: true);

            await using var operationScope =
                fixture.Services.CreateAsyncScope();

            var operationDb = operationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var coordinator = operationScope.ServiceProvider
                .GetRequiredService<IProductTypeSchemaCoordinator>();

            int attempts = 0;

            await coordinator.ExecuteCreationAsync(
                productType.Id,
                productType.ParentProductTypeId,
                async token =>
                {
                    attempts++;

                    operationDb.ProductTypes.Add(productType);
                    await operationDb.SaveChangesAsync(token);

                    ThrowTransientFailureAfterFirstSave(
                        injectTransientFailure,
                        attempts);
                },
                cancellationToken);

            Assert.Equal(injectTransientFailure ? 2 : 1, attempts);

            await using var verificationScope =
                fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var persistedTypes = await verificationDb.ProductTypes
                .AsNoTracking()
                .Where(item =>
                    item.TenantId == tenantId &&
                    item.Code == code)
                .ToListAsync(cancellationToken);

            ProductType persisted = Assert.Single(persistedTypes);

            Assert.Equal(productType.Id, persisted.Id);
            Assert.Equal(tenantId, persisted.TenantId);
            Assert.Null(persisted.ParentProductTypeId);
            Assert.True(persisted.IsAssignable);
            Assert.Equal(0L, persisted.OwnSchemaVersion);

            var schemaReader = verificationScope.ServiceProvider
                .GetRequiredService<IProductTypeEffectiveSchemaReader>();

            var schema = await schemaReader.GetAsync(
                productType.Id,
                cancellationToken);

            Assert.NotNull(schema);
            Assert.True(schema.EffectiveSchemaVersion > 0);
            Assert.Empty(schema.Attributes);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteSchemaChange_AfterSaveFailure_PersistsExactlyOneOption(
        bool injectTransientFailure)
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

            ProductTypeId productTypeId;
            AttributeDefinitionId attributeDefinitionId;

            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                var seedCoordinator = seedScope.ServiceProvider
                    .GetRequiredService<IProductTypeSchemaCoordinator>();

                ProductType productType = ProductType.CreateRoot(
                    tenantId,
                    ProductTypeCode.Create($"retry_option_{Guid.NewGuid():N}"),
                    isAssignable: true);

                AttributeDefinition definition = productType.DefineAttribute(
                    AttributeKey.Create("color"),
                    AttributeDataType.SingleSelect,
                    AttributeScope.VariantOption,
                    isRequired: false,
                    displayOrder: 0);

                productTypeId = productType.Id;
                attributeDefinitionId = definition.Id;

                await seedCoordinator.ExecuteCreationAsync(
                    productType.Id,
                    productType.ParentProductTypeId,
                    async token =>
                    {
                        seedDb.ProductTypes.Add(productType);
                        await seedDb.SaveChangesAsync(token);
                    },
                    cancellationToken);
            }

            await using var operationScope =
                fixture.Services.CreateAsyncScope();

            var operationDb = operationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var coordinator = operationScope.ServiceProvider
                .GetRequiredService<IProductTypeSchemaCoordinator>();

            int attempts = 0;
            AttributeOptionId? rolledBackOptionId = null;

            await coordinator.ExecuteSchemaChangeAsync(
                productTypeId,
                async token =>
                {
                    attempts++;

                    ProductType productType = await operationDb.ProductTypes
                        .Include(item => item.AttributeDefinitions)
                        .ThenInclude(definition => definition.Options)
                        .SingleAsync(
                            item => item.Id == productTypeId,
                            token);

                    AttributeOption option = productType.AddAttributeOption(
                        attributeDefinitionId,
                        AttributeOptionCode.Create("black"),
                        displayOrder: 0);

                    await operationDb.SaveChangesAsync(token);

                    if (injectTransientFailure && attempts == 1)
                    {
                        rolledBackOptionId = option.Id;
                    }

                    ThrowTransientFailureAfterFirstSave(
                        injectTransientFailure,
                        attempts);
                },
                cancellationToken);

            Assert.Equal(injectTransientFailure ? 2 : 1, attempts);

            await using var verificationScope =
                fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            ProductType persisted = await verificationDb.ProductTypes
                .AsNoTracking()
                .Include(item => item.AttributeDefinitions)
                .ThenInclude(definition => definition.Options)
                .SingleAsync(
                    item => item.Id == productTypeId,
                    cancellationToken);

            Assert.Equal(tenantId, persisted.TenantId);
            Assert.Equal(1L, persisted.OwnSchemaVersion);

            AttributeDefinition persistedDefinition = Assert.Single(
                persisted.AttributeDefinitions);

            Assert.Equal(attributeDefinitionId, persistedDefinition.Id);

            AttributeOption persistedOption = Assert.Single(
                persistedDefinition.Options);

            Assert.Equal("black", persistedOption.Code.Value);
            Assert.Equal(tenantId, persistedOption.TenantId);
            Assert.Equal(
                attributeDefinitionId,
                persistedOption.AttributeDefinitionId);
            Assert.False(persistedOption.IsDeprecated);

            if (injectTransientFailure)
            {
                Assert.True(rolledBackOptionId.HasValue);
                Assert.NotEqual(
                    rolledBackOptionId.Value,
                    persistedOption.Id);
            }

            var schemaReader = verificationScope.ServiceProvider
                .GetRequiredService<IProductTypeEffectiveSchemaReader>();

            var schema = await schemaReader.GetAsync(
                productTypeId,
                cancellationToken);

            Assert.NotNull(schema);
            Assert.True(schema.EffectiveSchemaVersion > 0);

            var effectiveAttribute = Assert.Single(schema.Attributes);

            Assert.Equal("color", effectiveAttribute.Key.Value);
            Assert.Equal(
                AttributeDataType.SingleSelect,
                effectiveAttribute.DataType);

            var effectiveOption = Assert.Single(effectiveAttribute.Options);

            Assert.Equal("black", effectiveOption.Code.Value);
            Assert.False(effectiveOption.IsDeprecated);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Execute_WithPendingChanges_RejectsWithoutDiscardingChanges(
        bool executeCreation)
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

            ProductTypeId existingProductTypeId;

            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                ProductType existing = ProductType.CreateRoot(
                    tenantId,
                    ProductTypeCode.Create($"guard_seed_{Guid.NewGuid():N}"),
                    isAssignable: true);

                seedDb.ProductTypes.Add(existing);
                await seedDb.SaveChangesAsync(cancellationToken);

                existingProductTypeId = existing.Id;
            }

            await using var operationScope =
                fixture.Services.CreateAsyncScope();

            var operationDb = operationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            var coordinator = operationScope.ServiceProvider
                .GetRequiredService<IProductTypeSchemaCoordinator>();

            ProductType pending = ProductType.CreateRoot(
                tenantId,
                ProductTypeCode.Create($"guard_pending_{Guid.NewGuid():N}"),
                isAssignable: true);

            operationDb.ProductTypes.Add(pending);

            ProductType creationTarget = ProductType.CreateRoot(
                tenantId,
                ProductTypeCode.Create($"guard_target_{Guid.NewGuid():N}"),
                isAssignable: true);

            int callbackCalls = 0;

            async Task PersistAsync(CancellationToken token)
            {
                callbackCalls++;

                if (executeCreation)
                {
                    operationDb.ProductTypes.Add(creationTarget);
                }

                await operationDb.SaveChangesAsync(token);
            }

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => executeCreation
                    ? coordinator.ExecuteCreationAsync(
                        creationTarget.Id,
                        creationTarget.ParentProductTypeId,
                        PersistAsync,
                        cancellationToken)
                    : coordinator.ExecuteSchemaChangeAsync(
                        existingProductTypeId,
                        PersistAsync,
                        cancellationToken));

            Assert.Equal(0, callbackCalls);
            Assert.Equal(EntityState.Added, operationDb.Entry(pending).State);
            Assert.True(operationDb.ChangeTracker.HasChanges());
            Assert.Null(operationDb.Database.CurrentTransaction);

            await using var verificationScope =
                fixture.Services.CreateAsyncScope();

            var verificationDb = verificationScope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            Assert.False(await verificationDb.ProductTypes
                .AsNoTracking()
                .AnyAsync(
                    item => item.Id == pending.Id,
                    cancellationToken));

            Assert.False(await verificationDb.ProductTypes
                .AsNoTracking()
                .AnyAsync(
                    item => item.Id == creationTarget.Id,
                    cancellationToken));

            ProductType persisted = await verificationDb.ProductTypes
                .AsNoTracking()
                .SingleAsync(
                    item => item.Id == existingProductTypeId,
                    cancellationToken);

            Assert.Equal(0L, persisted.OwnSchemaVersion);
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    private static void ThrowTransientFailureAfterFirstSave(
        bool injectTransientFailure,
        int attempts)
    {
        if (!injectTransientFailure || attempts != 1)
        {
            return;
        }

        var simulatedFailure = new NpgsqlException(
            "Injected failure after SaveChanges.",
            new TimeoutException("Simulated transient timeout."));

        Assert.True(simulatedFailure.IsTransient);

        throw simulatedFailure;
    }
}
