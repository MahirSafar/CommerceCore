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
public sealed class ProductTypeSchemaStageRetryIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteSchemaChange_WhenStageFails_RetriesWholeTransaction(
        bool failDuringRefresh)
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

            ProductType root = ProductType.CreateRoot(
                tenantId,
                ProductTypeCode.Create($"stage_root_{Guid.NewGuid():N}"),
                isAssignable: false);

            ProductType child = ProductType.CreateChild(
                tenantId,
                root.Id,
                ProductTypeCode.Create($"stage_child_{Guid.NewGuid():N}"),
                isAssignable: true);

            Dictionary<ProductTypeId, long> initialVersions;

            await using (var seedScope = fixture.Services.CreateAsyncScope())
            {
                var seedDb = seedScope.ServiceProvider
                    .GetRequiredService<CommerceCoreDbContext>();

                var seedCoordinator = seedScope.ServiceProvider
                    .GetRequiredService<IProductTypeSchemaCoordinator>();

                foreach (ProductType productType in new[] { root, child })
                {
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

                initialVersions = await seedDb.ProductTypeEffectiveSchemas
                    .AsNoTracking()
                    .Where(item => item.TenantId == tenantId)
                    .ToDictionaryAsync(
                        item => item.ProductTypeId,
                        item => item.EffectiveSchemaVersion,
                        cancellationToken);

                Assert.Equal(2, initialVersions.Count);
            }

            // Identifiers contain only a fixed prefix and a generated GUID.
            string prefix = $"stage_retry_{Guid.NewGuid():N}";
            string sequenceName = $"{prefix}_seq";
            string functionName = $"{prefix}_fail";
            string triggerName = $"{prefix}_trigger";
            string tableName = failDuringRefresh ? "product_type_effective_schema" : "product_types";
            string idColumn = failDuringRefresh ? "product_type_id" : "id";
            string triggerEvent = failDuringRefresh ? "AFTER INSERT OR UPDATE" : "AFTER UPDATE OF own_schema_version";
            Guid targetId = failDuringRefresh ? child.Id.Value : root.Id.Value;

            string installSql = $"""
                BEGIN;
                SET LOCAL lock_timeout = '3s';
                SET LOCAL statement_timeout = '10s';

                CREATE SEQUENCE catalog.{sequenceName}
                    START WITH 1
                    INCREMENT BY 1
                    CACHE 1;

                GRANT USAGE, SELECT ON SEQUENCE catalog.{sequenceName} TO commercecore_app;

                CREATE FUNCTION catalog.{functionName}()
                RETURNS trigger
                LANGUAGE plpgsql
                SECURITY INVOKER
                SET search_path = pg_catalog
                AS $function$
                BEGIN
                    IF nextval('catalog.{sequenceName}'::regclass) = 1 THEN
                        RAISE EXCEPTION USING
                            ERRCODE = '40001',
                            MESSAGE = 'Injected schema stage retry failure';
                    END IF;
                    RETURN NEW;
                END;
                $function$;

                REVOKE ALL ON FUNCTION catalog.{functionName}() FROM PUBLIC;
                GRANT EXECUTE ON FUNCTION catalog.{functionName}() TO commercecore_app;

                CREATE TRIGGER {triggerName}
                    {triggerEvent}
                    ON catalog.{tableName}
                    FOR EACH ROW
                    WHEN (
                        NEW.tenant_id = '{tenantId.Value:D}'::uuid AND
                        NEW.{idColumn} = '{targetId:D}'::uuid
                    )
                    EXECUTE FUNCTION catalog.{functionName}();

                COMMIT;
                """;

            string cleanupSql = $"""
                BEGIN;
                SET LOCAL lock_timeout = '3s';
                SET LOCAL statement_timeout = '10s';

                DROP TRIGGER IF EXISTS {triggerName} ON catalog.{tableName};
                DROP FUNCTION IF EXISTS catalog.{functionName}();
                DROP SEQUENCE IF EXISTS catalog.{sequenceName};

                COMMIT;
                """;

            try
            {
                await fixture.ExecuteTestAdminSqlAsync(
                    installSql,
                    cancellationToken);

                int attempts = 0;

                await using (var operationScope = fixture.Services.CreateAsyncScope())
                {
                    var operationDb = operationScope.ServiceProvider
                        .GetRequiredService<CommerceCoreDbContext>();

                    var coordinator = operationScope.ServiceProvider
                        .GetRequiredService<IProductTypeSchemaCoordinator>();

                    await coordinator.ExecuteSchemaChangeAsync(
                        root.Id,
                        async token =>
                        {
                            attempts++;

                            ProductType productType = await operationDb.ProductTypes
                                .Include(item => item.AttributeDefinitions)
                                .SingleAsync(
                                    item => item.Id == root.Id,
                                    token);

                            // These assertions also run on the second attempt:
                            // no mutation from the failed transaction may remain.
                            Assert.Empty(productType.AttributeDefinitions);
                            Assert.Equal(0L, productType.OwnSchemaVersion);

                            var observedVersions = await operationDb.ProductTypeEffectiveSchemas
                                .AsNoTracking()
                                .Where(item => item.TenantId == tenantId)
                                .ToDictionaryAsync(
                                    item => item.ProductTypeId,
                                    item => item.EffectiveSchemaVersion,
                                    token);

                            Assert.Equal(2, observedVersions.Count);
                            Assert.Equal(
                                initialVersions[root.Id],
                                observedVersions[root.Id]);
                            Assert.Equal(
                                initialVersions[child.Id],
                                observedVersions[child.Id]);

                            productType.DefineAttribute(
                                AttributeKey.Create("stage_attribute"),
                                AttributeDataType.Text,
                                AttributeScope.ProductSpecification,
                                isRequired: false,
                                displayOrder: 0);

                            await operationDb.SaveChangesAsync(token);
                        },
                        cancellationToken);
                }

                Assert.Equal(2, attempts);

                await using (var verificationScope = fixture.Services.CreateAsyncScope())
                {
                    var verificationDb = verificationScope.ServiceProvider
                        .GetRequiredService<CommerceCoreDbContext>();

                    var persistedTypes = await verificationDb.ProductTypes
                        .AsNoTracking()
                        .Include(item => item.AttributeDefinitions)
                        .Where(item => item.TenantId == tenantId)
                        .ToDictionaryAsync(
                            item => item.Id,
                            cancellationToken);

                    Assert.Equal(2, persistedTypes.Count);

                    ProductType persistedRoot = persistedTypes[root.Id];
                    ProductType persistedChild = persistedTypes[child.Id];

                    Assert.Equal(1L, persistedRoot.OwnSchemaVersion);
                    Assert.Equal(0L, persistedChild.OwnSchemaVersion);
                    Assert.Empty(persistedChild.AttributeDefinitions);

                    AttributeDefinition definition = Assert.Single(
                        persistedRoot.AttributeDefinitions);

                    Assert.Equal("stage_attribute", definition.Key.Value);
                    Assert.Equal(tenantId, definition.TenantId);

                    var reader = verificationScope.ServiceProvider
                        .GetRequiredService<IProductTypeEffectiveSchemaReader>();

                    var rootSchema = await reader.GetAsync(
                        root.Id,
                        cancellationToken);

                    var childSchema = await reader.GetAsync(
                        child.Id,
                        cancellationToken);

                    Assert.NotNull(rootSchema);
                    Assert.NotNull(childSchema);

                    var rootAttribute = Assert.Single(rootSchema.Attributes);
                    var childAttribute = Assert.Single(childSchema.Attributes);

                    Assert.Equal("stage_attribute", rootAttribute.Key.Value);
                    Assert.Equal("stage_attribute", childAttribute.Key.Value);
                    Assert.Equal(AttributeDataType.Text, rootAttribute.DataType);
                    Assert.Equal(AttributeDataType.Text, childAttribute.DataType);

                    Assert.Equal(
                        rootSchema.EffectiveSchemaVersion,
                        childSchema.EffectiveSchemaVersion);

                    Assert.True(
                        rootSchema.EffectiveSchemaVersion > initialVersions.Values.Max());

                    // One failing trigger invocation and one successful invocation.
                    await verificationDb.Database.OpenConnectionAsync(
                        cancellationToken);

                    await using var counterCommand = new NpgsqlCommand(
                        $"SELECT last_value FROM catalog.{sequenceName};",
                        (NpgsqlConnection)verificationDb.Database.GetDbConnection());

                    object? counter = await counterCommand.ExecuteScalarAsync(
                        cancellationToken);

                    Assert.Equal(2L, Assert.IsType<long>(counter));
                }
            }
            finally
            {
                // Cleanup also runs after assertion failure or cancellation.
                // The admin helper has a bounded command timeout.
                await fixture.ExecuteTestAdminSqlAsync(
                    cleanupSql,
                    CancellationToken.None);
            }
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }
}
