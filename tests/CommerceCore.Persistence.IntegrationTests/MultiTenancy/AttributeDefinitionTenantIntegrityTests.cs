using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

[Collection(nameof(PostgreSqlCollection))]
public sealed class AttributeDefinitionTenantIntegrityTests(
    PostgreSqlFixture fixture)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Insert_Enforces_ProductType_Tenant(bool crossTenant) =>
        VerifyTenantIntegrityAsync(
            isUpdate: false,
            crossTenant: crossTenant);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task Update_Enforces_ProductType_Tenant(bool crossTenant) =>
        VerifyTenantIntegrityAsync(
            isUpdate: true,
            crossTenant: crossTenant);

    private async Task VerifyTenantIntegrityAsync(
        bool isUpdate,
        bool crossTenant)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        var tenantContext = fixture.Services
            .GetRequiredService<TestTenantContext>();

        TenantId? previousTenant = tenantContext.TenantId;
        TenantId tenantA = fixture.PrimaryTenantId;

        try
        {
            Guid sourceTypeId = await CreateProductTypeAsync(
                tenantA,
                cancellationToken);

            TenantId targetTenant = crossTenant
                ? fixture.SecondaryTenantId
                : tenantA;

            Guid targetTypeId = await CreateProductTypeAsync(
                targetTenant,
                cancellationToken);

            tenantContext.SetTenant(tenantA);

            await using var scope = fixture.Services.CreateAsyncScope();

            var db = scope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            // Uses the fixture's runtime connection and tenant interceptor.
            await db.Database.OpenConnectionAsync(cancellationToken);

            var connection =
                (NpgsqlConnection)db.Database.GetDbConnection();

            await using (var roleCommand = new NpgsqlCommand(
                """
                SELECT
                    current_user = 'commercecore_app'
                    AND NOT rolsuper
                    AND NOT rolbypassrls
                FROM pg_roles
                WHERE rolname = current_user
                """,
                connection))
            {
                object? restrictedRole =
                    await roleCommand.ExecuteScalarAsync(cancellationToken);

                Assert.True(restrictedRole is true);
            }

            // Direct SQL tests the database boundary, not domain validation.
            // Disposal rolls back even when the expected rejection is absent.
            await using var transaction =
                await connection.BeginTransactionAsync(cancellationToken);

            Guid definitionId = Guid.NewGuid();

            if (isUpdate)
            {
                int inserted = await InsertDefinitionAsync(
                    connection,
                    transaction,
                    definitionId,
                    tenantA.Value,
                    sourceTypeId,
                    cancellationToken);

                Assert.Equal(1, inserted);
            }

            async Task<int> ExecuteWriteAsync()
            {
                if (!isUpdate)
                {
                    return await InsertDefinitionAsync(
                        connection,
                        transaction,
                        definitionId,
                        tenantA.Value,
                        targetTypeId,
                        cancellationToken);
                }

                await using var command = new NpgsqlCommand(
                    """
                    UPDATE catalog.attribute_definitions
                    SET product_type_id = @product_type_id
                    WHERE id = @id
                      AND tenant_id = @tenant_id
                    """,
                    connection,
                    transaction);

                command.Parameters.AddWithValue(
                    "product_type_id", targetTypeId);
                command.Parameters.AddWithValue(
                    "id", definitionId);
                command.Parameters.AddWithValue(
                    "tenant_id", tenantA.Value);

                return await command.ExecuteNonQueryAsync(
                    cancellationToken);
            }

            if (crossTenant)
            {
                PostgresException exception =
                    await Assert.ThrowsAsync<PostgresException>(
                        async () =>
                        {
                            await ExecuteWriteAsync();
                        });

                Assert.Equal(
                    PostgresErrorCodes.ForeignKeyViolation,
                    exception.SqlState);

                Assert.Equal(
                    "fk_attribute_definitions_product_type",
                    exception.ConstraintName);
            }
            else
            {
                int affected = await ExecuteWriteAsync();

                Assert.Equal(1, affected);
            }
        }
        finally
        {
            tenantContext.SetTenant(previousTenant);
        }
    }

    private async Task<Guid> CreateProductTypeAsync(
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        fixture.Services.GetRequiredService<TestTenantContext>()
            .SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();

        var db = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        ProductType productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"fk_test_{Guid.NewGuid():N}"));

        db.ProductTypes.Add(productType);

        await db.SaveChangesAsync(cancellationToken);

        return productType.Id.Value;
    }

    private static async Task<int> InsertDefinitionAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid definitionId,
        Guid tenantId,
        Guid productTypeId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO catalog.attribute_definitions
                (id, tenant_id, product_type_id, key,
                 data_type, scope, is_required,
                 enforcement_status, is_deprecated, display_order)
            VALUES
                (@id, @tenant_id, @product_type_id, 'test_attribute',
                 'Text', 'ProductSpecification', false,
                 'Enforced', false, 0)
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("id", definitionId);
        command.Parameters.AddWithValue("tenant_id", tenantId);
        command.Parameters.AddWithValue("product_type_id", productTypeId);

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
