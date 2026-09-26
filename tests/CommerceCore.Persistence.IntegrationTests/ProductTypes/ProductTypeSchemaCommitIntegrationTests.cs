using System.Data.Common;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.ProductTypes;

[Collection(nameof(PostgreSqlCollection))]
public sealed class ProductTypeSchemaCommitIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteSchemaChange_WhenCommitThrows_DoesNotReplay(
        bool throwAfterCommit)
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
            long initialSchemaVersion;
            string applicationConnectionString;

            await using (var seedScope =
                fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                applicationConnectionString =
                    seedDb.Database.GetConnectionString()
                    ?? throw new InvalidOperationException(
                        "The fixture application connection is missing.");

                var seedCoordinator = seedScope.ServiceProvider
                    .GetRequiredService<IProductTypeSchemaCoordinator>();

                ProductType productType = ProductType.CreateRoot(
                    tenantId,
                    ProductTypeCode.Create($"commit_{Guid.NewGuid():N}"),
                    isAssignable: true);

                productTypeId = productType.Id;

                await seedCoordinator.ExecuteCreationAsync(
                    productType.Id,
                    productType.ParentProductTypeId,
                    async token =>
                    {
                        seedDb.ProductTypes.Add(productType);
                        await seedDb.SaveChangesAsync(token);
                    },
                    cancellationToken);

                var seedReader = seedScope.ServiceProvider
                    .GetRequiredService<IProductTypeEffectiveSchemaReader>();

                var initialSchema = await seedReader.GetAsync(
                    productTypeId,
                    cancellationToken);

                Assert.NotNull(initialSchema);
                Assert.Empty(initialSchema.Attributes);

                initialSchemaVersion =
                    initialSchema.EffectiveSchemaVersion;
            }

            var interceptor = new CommitFailureInterceptor(
                throwAfterCommit);

            Assert.True(interceptor.Failure.IsTransient);

            int callbackCalls = 0;

            // Only the operation provider receives the fault injector.
            // Seed and verification use the normal fixture services.
            await using (ServiceProvider operationServices =
                CreateOperationServices(
                    applicationConnectionString,
                    tenantContext,
                    interceptor))
            {
                await using var operationScope =
                    operationServices.CreateAsyncScope();

                var operationDb = operationScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                var coordinator = operationScope.ServiceProvider
                    .GetRequiredService<IProductTypeSchemaCoordinator>();

                InvalidOperationException failure =
                    await Assert.ThrowsAsync<InvalidOperationException>(
                        () => coordinator.ExecuteSchemaChangeAsync(
                            productTypeId,
                            async token =>
                            {
                                callbackCalls++;

                                ProductType productType =
                                    await operationDb.ProductTypes
                                        .Include(item =>
                                            item.AttributeDefinitions)
                                        .SingleAsync(
                                            item => item.Id == productTypeId,
                                            token);

                                productType.DefineAttribute(
                                    AttributeKey.Create("commit_attribute"),
                                    AttributeDataType.Text,
                                    AttributeScope.ProductSpecification,
                                    isRequired: false,
                                    displayOrder: 0);

                                await operationDb.SaveChangesAsync(token);
                            },
                            cancellationToken));

                Assert.Same(interceptor.Failure, failure.InnerException);
            }

            Assert.Equal(1, callbackCalls);
            Assert.Equal(1, interceptor.CommitAttempts);
            Assert.Equal(1, interceptor.InjectedFailures);
            Assert.Equal(
                throwAfterCommit ? 1 : 0,
                interceptor.SuccessfulCommits);

            // The failed operation scope has been disposed.
            // Inspect the actual committed state with a fresh context.
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

            Assert.Equal(tenantId, persisted.TenantId);
            Assert.Equal(
                throwAfterCommit ? 1L : 0L,
                persisted.OwnSchemaVersion);

            var schemaReader = verificationScope.ServiceProvider
                .GetRequiredService<IProductTypeEffectiveSchemaReader>();

            var schema = await schemaReader.GetAsync(
                productTypeId,
                cancellationToken);

            Assert.NotNull(schema);

            if (throwAfterCommit)
            {
                AttributeDefinition definition = Assert.Single(
                    persisted.AttributeDefinitions);

                Assert.Equal("commit_attribute", definition.Key.Value);
                Assert.Equal(tenantId, definition.TenantId);

                var effectiveAttribute = Assert.Single(schema.Attributes);

                Assert.Equal(
                    "commit_attribute",
                    effectiveAttribute.Key.Value);
                Assert.Equal(
                    AttributeDataType.Text,
                    effectiveAttribute.DataType);
                Assert.True(
                    schema.EffectiveSchemaVersion > initialSchemaVersion);
            }
            else
            {
                Assert.Empty(persisted.AttributeDefinitions);
                Assert.Empty(schema.Attributes);
                Assert.Equal(
                    initialSchemaVersion,
                    schema.EffectiveSchemaVersion);
            }
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    private ServiceProvider CreateOperationServices(
        string connectionString,
        TestTenantContext tenantContext,
        IInterceptor interceptor)
    {
        var services = new ServiceCollection();

        services.AddSingleton(
            fixture.Services.GetRequiredService<IClock>());

        services.AddSingleton(
            fixture.Services.GetRequiredService<ICurrentUser>());

        services.AddSingleton<ITenantContext>(tenantContext);

        // Keep the production registrations, runtime role,
        // tenant interceptor, auditing and retry configuration.
        services.AddPersistence(connectionString);

        services.ConfigureDbContext<CommerceCoreDbContext>(
            options => options.AddInterceptors(interceptor));

        return services.BuildServiceProvider(
            new ServiceProviderOptions
            {
                ValidateScopes = true,
                ValidateOnBuild = true
            });
    }

    private sealed class CommitFailureInterceptor(
        bool throwAfterCommit) : DbTransactionInterceptor
    {
        public NpgsqlException Failure { get; } = new(
            "Injected transaction commit failure.",
            new TimeoutException("Simulated commit timeout."));

        public int CommitAttempts { get; private set; }

        public int SuccessfulCommits { get; private set; }

        public int InjectedFailures { get; private set; }

        public override ValueTask<InterceptionResult>
            TransactionCommittingAsync(
                DbTransaction transaction,
                TransactionEventData eventData,
                InterceptionResult result,
                CancellationToken cancellationToken = default)
        {
            CommitAttempts++;

            if (!throwAfterCommit && InjectedFailures == 0)
            {
                InjectedFailures++;
                throw Failure;
            }

            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            SuccessfulCommits++;

            if (throwAfterCommit && InjectedFailures == 0)
            {
                InjectedFailures++;
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }
}
