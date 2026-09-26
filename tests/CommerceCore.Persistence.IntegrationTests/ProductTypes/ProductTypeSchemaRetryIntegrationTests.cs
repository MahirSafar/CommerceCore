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
}
