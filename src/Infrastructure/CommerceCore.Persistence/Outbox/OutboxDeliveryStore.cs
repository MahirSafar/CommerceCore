using System.Data;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CommerceCore.Persistence.Outbox;

public sealed record LeasedOutboxMessage(
    Guid Id,
    TenantId TenantId,
    Guid LeaseId,
    int AttemptCount,
    string Type,
    string Content);

public sealed class OutboxDeliveryStore(
    CommerceCoreDbContext dbContext,
    ITenantContext tenantContext)
{
    private const int MaximumAttempts = 5;
    private const int ExhaustedBatchSize = 100;
    private const int InitialRetryDelaySeconds = 5;
    private const int MaximumRetryDelaySeconds = 300;

    private readonly CommerceCoreDbContext _dbContext = dbContext;
    private readonly ITenantContext _tenantContext = tenantContext;

    public Task<LeasedOutboxMessage?> ClaimAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromMinutes(30))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        return ExecuteAsync<LeasedOutboxMessage?>(
            async (connection, tenantId) =>
            {
                await DeadLetterExhaustedAsync(
                    connection,
                    tenantId,
                    cancellationToken);

                Guid leaseId = Guid.NewGuid();

                await using var command = connection.CreateCommand();

                command.CommandText = """
                    WITH candidate AS (
                        SELECT id
                        FROM outbox.messages
                        WHERE tenant_id = @tenant_id
                            AND attempt_count < @maximum_attempts
                            AND processed_on_utc IS NULL
                            AND dead_lettered_on_utc IS NULL
                            AND (
                                next_attempt_on_utc IS NULL
                                OR next_attempt_on_utc <= statement_timestamp()
                            )
                            AND (
                                lease_expires_on_utc IS NULL
                                OR lease_expires_on_utc <= statement_timestamp()
                            )
                        ORDER BY next_attempt_on_utc NULLS FIRST, occurred_on_utc, id
                        LIMIT 1
                        FOR UPDATE SKIP LOCKED
                    )
                    UPDATE outbox.messages AS message
                    SET lease_id = @lease_id,
                        lease_expires_on_utc = statement_timestamp() + @lease_duration,
                        next_attempt_on_utc = NULL,
                        attempt_count = message.attempt_count + 1
                    FROM candidate
                    WHERE message.id = candidate.id
                        AND message.tenant_id = @tenant_id
                    RETURNING message.id, message.attempt_count, message.type, message.content;
                    """;

                command.Parameters.AddWithValue("tenant_id", tenantId.Value);
                command.Parameters.AddWithValue("maximum_attempts", MaximumAttempts);
                command.Parameters.AddWithValue("lease_id", leaseId);
                command.Parameters.AddWithValue("lease_duration", leaseDuration);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);

                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                return new LeasedOutboxMessage(
                    reader.GetGuid(0),
                    tenantId,
                    leaseId,
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3));
            },
            cancellationToken);
    }

    public Task<bool> CompleteAsync(
        Guid messageId,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        return ExecuteAsync(
            async (connection, tenantId) =>
            {
                await using var command = connection.CreateCommand();

                command.CommandText = """
                    UPDATE outbox.messages
                    SET processed_on_utc = statement_timestamp(),
                        lease_id = NULL,
                        lease_expires_on_utc = NULL,
                        next_attempt_on_utc = NULL,
                        last_error = NULL
                    WHERE id = @message_id
                        AND tenant_id = @tenant_id
                        AND lease_id = @lease_id
                        AND lease_expires_on_utc > clock_timestamp()
                        AND processed_on_utc IS NULL
                        AND dead_lettered_on_utc IS NULL;
                    """;

                command.Parameters.AddWithValue("message_id", messageId);
                command.Parameters.AddWithValue("tenant_id", tenantId.Value);
                command.Parameters.AddWithValue("lease_id", leaseId);

                int affectedRows = await command.ExecuteNonQueryAsync(cancellationToken);

                return affectedRows == 1;
            },
            cancellationToken);
    }

    public Task<bool> FailAsync(
        Guid messageId,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(errorCode);

        if (errorCode.Length > 200)
        {
            throw new ArgumentOutOfRangeException(nameof(errorCode));
        }

        return ExecuteAsync(
            async (connection, tenantId) =>
            {
                await using var command = connection.CreateCommand();

                command.CommandText = """
                    UPDATE outbox.messages
                    SET last_error = @error_code,
                        lease_id = NULL,
                        lease_expires_on_utc = NULL,
                        dead_lettered_on_utc = CASE
                            WHEN attempt_count >= @maximum_attempts
                            THEN statement_timestamp()
                            ELSE NULL
                        END,
                        next_attempt_on_utc = CASE
                            WHEN attempt_count >= @maximum_attempts
                            THEN NULL
                            ELSE statement_timestamp()
                                + LEAST(
                                    @maximum_delay,
                                    @initial_delay * power(
                                        2.0,
                                        LEAST(attempt_count - 1, 16)
                                    )
                                ) * interval '1 second'
                        END
                    WHERE id = @message_id
                      AND tenant_id = @tenant_id
                      AND lease_id = @lease_id
                      AND lease_expires_on_utc > clock_timestamp()
                      AND processed_on_utc IS NULL
                      AND dead_lettered_on_utc IS NULL;
                    """;

                command.Parameters.AddWithValue("message_id", messageId);
                command.Parameters.AddWithValue("tenant_id", tenantId.Value);
                command.Parameters.AddWithValue("lease_id", leaseId);
                command.Parameters.AddWithValue("error_code", errorCode);
                command.Parameters.AddWithValue("maximum_attempts", MaximumAttempts);
                command.Parameters.AddWithValue(
                    "initial_delay",
                    InitialRetryDelaySeconds);
                command.Parameters.AddWithValue(
                    "maximum_delay",
                    MaximumRetryDelaySeconds);

                return await command.ExecuteNonQueryAsync(cancellationToken) == 1;
            },
            cancellationToken);
    }

    private static async Task DeadLetterExhaustedAsync(
        NpgsqlConnection connection,
        TenantId tenantId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();

        command.CommandText = """
            WITH exhausted AS (
                SELECT id
                FROM outbox.messages
                WHERE tenant_id = @tenant_id
                  AND processed_on_utc IS NULL
                  AND dead_lettered_on_utc IS NULL
                  AND attempt_count >= @maximum_attempts
                  AND (
                      lease_expires_on_utc IS NULL
                      OR lease_expires_on_utc <= statement_timestamp()
                  )
                ORDER BY occurred_on_utc, id
                LIMIT @batch_size
                FOR UPDATE SKIP LOCKED
            )
            UPDATE outbox.messages AS message
            SET dead_lettered_on_utc = statement_timestamp(),
                lease_id = NULL,
                lease_expires_on_utc = NULL,
                next_attempt_on_utc = NULL,
                last_error = 'delivery_attempts_exhausted'
            FROM exhausted
            WHERE message.id = exhausted.id
              AND message.tenant_id = @tenant_id;
            """;

        command.Parameters.AddWithValue("tenant_id", tenantId.Value);
        command.Parameters.AddWithValue("maximum_attempts", MaximumAttempts);
        command.Parameters.AddWithValue("batch_size", ExhaustedBatchSize);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<T> ExecuteAsync<T>(
        Func<NpgsqlConnection, TenantId, Task<T>> action,
        CancellationToken cancellationToken)
    {
        TenantId tenantId = _tenantContext.TenantId ?? throw new InvalidOperationException(
            "Outbox delivery requires a tenant context.");

        if (_dbContext.Database.CurrentTransaction is not null || _dbContext.Database.GetDbConnection().State != ConnectionState.Closed)
        {
            throw new InvalidOperationException(
                "Outbox delivery requires a dedicated context with a closed connection.");
        }

        await _dbContext.Database.OpenConnectionAsync(cancellationToken);

        try
        {
            var connection = (NpgsqlConnection)_dbContext.Database.GetDbConnection();

            return await action(connection, tenantId);
        }
        finally
        {
            await _dbContext.Database.CloseConnectionAsync();
        }
    }
}
