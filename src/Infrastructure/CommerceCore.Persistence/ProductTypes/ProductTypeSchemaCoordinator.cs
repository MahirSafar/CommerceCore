using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace CommerceCore.Persistence.ProductTypes;

internal sealed class ProductTypeSchemaCoordinator(CommerceCoreDbContext dbContext) : IProductTypeSchemaCoordinator
{
    private readonly CommerceCoreDbContext _dbContext = dbContext;

    public Task ExecuteCreationAsync(
        ProductTypeId newProductTypeId,
        ProductTypeId? parentProductTypeId,
        Func<CancellationToken, Task> persistAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistAsync);

        return ExecuteInTransactionAsync(
            async transaction =>
            {
                Guid treeRootId = parentProductTypeId.HasValue
                    ? await GetTreeRootIdAsync(
                        parentProductTypeId.Value,
                        transaction,
                        cancellationToken)
                    : newProductTypeId.Value;

                await AcquireTreeAdvisoryLockAsync(
                    treeRootId,
                    transaction,
                    cancellationToken);

                await persistAsync(cancellationToken);

                IReadOnlyList<Guid> targetIds = await LockSubtreeAsync(
                    newProductTypeId,
                    transaction,
                    cancellationToken);

                long revision = await GetNextRevisionAsync(
                    transaction,
                    cancellationToken);

                await RefreshEffectiveSchemasAsync(
                    targetIds,
                    revision,
                    transaction,
                    cancellationToken);
            },
            cancellationToken);
    }

    public Task ExecuteSchemaChangeAsync(
        ProductTypeId affectedProductTypeId,
        Func<CancellationToken, Task> persistAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(persistAsync);

        return ExecuteInTransactionAsync(
            async transaction =>
            {
                Guid treeRootId = await GetTreeRootIdAsync(
                    affectedProductTypeId,
                    transaction,
                    cancellationToken);

                await AcquireTreeAdvisoryLockAsync(
                    treeRootId,
                    transaction,
                    cancellationToken);

                IReadOnlyList<Guid> targetIds = await LockSubtreeAsync(
                    affectedProductTypeId,
                    transaction,
                    cancellationToken);

                await persistAsync(cancellationToken);

                await IncrementOwnSchemaVersionAsync(
                    affectedProductTypeId,
                    transaction,
                    cancellationToken);

                long revision = await GetNextRevisionAsync(
                    transaction,
                    cancellationToken);

                await RefreshEffectiveSchemasAsync(
                    targetIds,
                    revision,
                    transaction,
                    cancellationToken);
            },
            cancellationToken);
    }

    private async Task ExecuteInTransactionAsync(
        Func<IDbContextTransaction, Task> action,
        CancellationToken cancellationToken)
    {
        IExecutionStrategy executionStrategy =
            _dbContext.Database.CreateExecutionStrategy();

        await executionStrategy.ExecuteAsync(async () =>
        {
            await using IDbContextTransaction transaction = await _dbContext.Database
                .BeginTransactionAsync(cancellationToken);

            try
            {
                await action(transaction);
                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        });
    }

    private async Task<Guid> GetTreeRootIdAsync(
        ProductTypeId productTypeId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            """
            SELECT root.id
            FROM catalog.product_types AS target
            INNER JOIN catalog.product_types AS root
                ON root.path @> target.path
            WHERE target.id = @product_type_id
            ORDER BY nlevel(root.path) ASC
            LIMIT 1;
            """);

        command.Parameters.Add(
            new NpgsqlParameter(
                "product_type_id",
                NpgsqlDbType.Uuid)
            {
                Value = productTypeId.Value
            });

        object? result = await command.ExecuteScalarAsync(cancellationToken);

        return result is Guid rootId
            ? rootId
            : throw new InvalidOperationException(
                $"Product type '{productTypeId}' was not found.");
    }

    private async Task AcquireTreeAdvisoryLockAsync(
        Guid treeRootId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            """
            SELECT pg_advisory_xact_lock(
                hashtextextended(@tree_root_id::text, 0));
            """);

        command.Parameters.Add(
            new NpgsqlParameter(
                "tree_root_id",
                NpgsqlDbType.Uuid)
            {
                Value = treeRootId
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<Guid>> LockSubtreeAsync(
        ProductTypeId productTypeId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            """
            SELECT node.id
            FROM catalog.product_types AS node
            WHERE node.path <@ (
                SELECT target.path
                FROM catalog.product_types AS target
                WHERE target.id = @product_type_id
            )
            ORDER BY node.id
            FOR UPDATE;
            """);

        command.Parameters.Add(
            new NpgsqlParameter(
                "product_type_id",
                NpgsqlDbType.Uuid)
            {
                Value = productTypeId.Value
            });

        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(
            cancellationToken);

        List<Guid> productTypeIds = [];

        while (await reader.ReadAsync(cancellationToken))
        {
            productTypeIds.Add(reader.GetGuid(0));
        }

        if (productTypeIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Product type '{productTypeId}' was not found.");
        }

        return productTypeIds;
    }

    private async Task IncrementOwnSchemaVersionAsync(
        ProductTypeId productTypeId,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            """
            UPDATE catalog.product_types
            SET own_schema_version = own_schema_version + 1
            WHERE id = @product_type_id;
            """);

        command.Parameters.Add(
            new NpgsqlParameter(
                "product_type_id",
                NpgsqlDbType.Uuid)
            {
                Value = productTypeId.Value
            });

        int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

        if (affectedRows != 1)
        {
            throw new InvalidOperationException(
                $"Product type '{productTypeId}' was not found.");
        }
    }

    private async Task<long> GetNextRevisionAsync(
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            "SELECT nextval('catalog.schema_revision_seq');");

        object? result = await command.ExecuteScalarAsync(cancellationToken);

        return result is long revision
            ? revision
            : throw new InvalidOperationException(
                "Could not obtain the next product-type schema revision.");
    }

    private async Task RefreshEffectiveSchemasAsync(
        IReadOnlyList<Guid> productTypeIds,
        long revision,
        IDbContextTransaction transaction,
        CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = CreateCommand(
            transaction,
            """
            INSERT INTO catalog.product_type_effective_schema (
                tenant_id,
                product_type_id,
                effective_schema_version,
                schema,
                updated_at_utc)
            SELECT
                target.tenant_id,
                target.id,
                @schema_revision,
                jsonb_build_object(
                    'attributes',
                    COALESCE(
                        (
                            SELECT jsonb_agg(
                                jsonb_build_object(
                                    'id', selected.id,
                                    'key', selected.key,
                                    'dataType', selected.data_type,
                                    'scope', selected.scope,
                                    'isRequired', selected.is_required,
                                    'enforcementStatus',
                                        selected.enforcement_status,
                                    'isDeprecated', selected.is_deprecated,
                                    'displayOrder', selected.display_order,
                                    'minimumValue', selected.minimum_value,
                                    'maximumValue', selected.maximum_value,
                                    'minimumLength', selected.minimum_length,
                                    'maximumLength', selected.maximum_length,
                                    'measurementUnitFamily',
                                        selected.measurement_unit_family,
                                    'options',
                                        COALESCE(
                                            (
                                                SELECT jsonb_agg(
                                                    jsonb_build_object(
                                                        'id', option.id,
                                                        'code', option.code,
                                                        'displayOrder',
                                                            option.display_order,
                                                        'isDeprecated',
                                                            option.is_deprecated)
                                                    ORDER BY
                                                        option.display_order,
                                                        option.code)
                                                FROM catalog.attribute_options
                                                    AS option
                                                WHERE option.attribute_definition_id =
                                                    selected.id
                                            ),
                                            '[]'::jsonb)
                                )
                                ORDER BY
                                    selected.display_order,
                                    selected.key
                            )
                            FROM (
                                SELECT DISTINCT ON (definition.key)
                                    definition.id,
                                    definition.key,
                                    definition.data_type,
                                    definition.scope,
                                    definition.is_required,
                                    definition.enforcement_status,
                                    definition.is_deprecated,
                                    definition.display_order,
                                    definition.minimum_value,
                                    definition.maximum_value,
                                    definition.minimum_length,
                                    definition.maximum_length,
                                    definition.measurement_unit_family
                                FROM catalog.attribute_definitions AS definition
                                INNER JOIN catalog.product_types AS schema_owner
                                    ON schema_owner.id = definition.product_type_id
                                WHERE schema_owner.path @> target.path
                                ORDER BY
                                    definition.key,
                                    nlevel(schema_owner.path) DESC
                            ) AS selected
                        ),
                        '[]'::jsonb)
                ),
                CURRENT_TIMESTAMP
            FROM catalog.product_types AS target
            WHERE target.id = ANY(@product_type_ids)
            ON CONFLICT (tenant_id, product_type_id)
            DO UPDATE SET
                effective_schema_version = EXCLUDED.effective_schema_version,
                schema = EXCLUDED.schema,
                updated_at_utc = EXCLUDED.updated_at_utc;
            """);

        command.Parameters.Add(
            new NpgsqlParameter(
                "product_type_ids",
                NpgsqlDbType.Array | NpgsqlDbType.Uuid)
            {
                Value = productTypeIds.ToArray()
            });

        command.Parameters.Add(
            new NpgsqlParameter(
                "schema_revision",
                NpgsqlDbType.Bigint)
            {
                Value = revision
            });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private NpgsqlCommand CreateCommand(
        IDbContextTransaction transaction,
        string commandText)
    {
        NpgsqlConnection connection = _dbContext.Database.GetDbConnection()
            as NpgsqlConnection
            ?? throw new InvalidOperationException(
                "CommerceCore requires an Npgsql PostgreSQL connection.");

        NpgsqlTransaction dbTransaction = transaction.GetDbTransaction()
            as NpgsqlTransaction
            ?? throw new InvalidOperationException(
                "CommerceCore requires an Npgsql PostgreSQL transaction.");

        return new NpgsqlCommand(
            commandText,
            connection,
            dbTransaction);
    }
}