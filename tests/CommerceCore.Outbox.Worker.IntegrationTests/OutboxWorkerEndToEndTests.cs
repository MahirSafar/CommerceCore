using System.Globalization;
using System.Text;
using System.Text.Json;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Persistence;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Npgsql;
using RabbitMQ.Client;
using Testcontainers.PostgreSql;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class OutboxWorkerEndToEndTests(RabbitMqFixture rabbitMq)
    : IClassFixture<RabbitMqFixture>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Worker_PublishesActiveTenants_AndLeavesInactiveTenantPending(
        bool delayedBinding)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await using var postgres = new PostgreSqlBuilder("postgres:18.6")
            .WithDatabase("outbox_e2e")
            .WithUsername("postgres")
            .WithPassword("DisposableAdminPassword!")
            .Build();

        await postgres.StartAsync(token);

        var adminOptions = new DbContextOptionsBuilder<CommerceCoreDbContext>()
            .UseNpgsql(postgres.GetConnectionString())
            .Options;

        await using var adminDb = new CommerceCoreDbContext(adminOptions);
        await adminDb.Database.MigrateAsync(token);

        OutboxMessage[] seeded = await SeedAsync(adminDb, token);
        string runtimeConnection = await CreateRuntimeRoleAsync(
            adminDb,
            postgres.GetConnectionString(),
            token);

        // Runtime rolu tenant sessiyası olmadan mesajları görə bilməməlidir.
        var runtimeOptions = new DbContextOptionsBuilder<CommerceCoreDbContext>()
            .UseNpgsql(runtimeConnection)
            .Options;

        await using (var unscopedDb = new CommerceCoreDbContext(runtimeOptions))
        {
            Assert.Equal(0, await unscopedDb.OutboxMessages.CountAsync(token));
        }

        string exchangeName = $"e2e.events.{Guid.NewGuid():N}";
        string queueName = $"e2e.queue.{Guid.NewGuid():N}";

        await using IConnection connection = await rabbitMq.ConnectAsync(token);
        await using IChannel channel = await connection.CreateChannelAsync(
            cancellationToken: token);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: token);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            },
            cancellationToken: token);

        if (!delayedBinding)
        {
            await channel.QueueBindAsync(
                queue: queueName,
                exchange: exchangeName,
                routingKey: "catalog.product.created.v1",
                cancellationToken: token);
        }

        RabbitMqOptions rabbitOptions = rabbitMq.CreateOptions(exchangeName);

        HostApplicationBuilder builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                DisableDefaults = true
            });

        builder.Configuration.AddInMemoryCollection(
            new Dictionary<string, string?>
            {
                ["ConnectionStrings:CommerceCoreDatabase"] = runtimeConnection,
                ["RabbitMq:HostName"] = rabbitOptions.HostName,
                ["RabbitMq:Port"] =
                    rabbitOptions.Port.ToString(CultureInfo.InvariantCulture),
                ["RabbitMq:UserName"] = rabbitOptions.UserName,
                ["RabbitMq:Password"] = rabbitOptions.Password,
                ["RabbitMq:VirtualHost"] = rabbitOptions.VirtualHost,
                ["RabbitMq:ExchangeName"] = exchangeName,
                ["RabbitMq:DispatcherEnabled"] = "true"
            });

        builder.AddOutboxWorker();

        using IHost host = builder.Build();

        try
        {
            await host.StartAsync(token);

            if (delayedBinding)
            {
                OutboxMessage failed = await WaitForRetryAsync(
                    adminDb,
                    seeded[0].Id,
                    token);

                Assert.Null(failed.ProcessedOnUtc);
                Assert.Null(failed.DeadLetteredOnUtc);
                Assert.Null(failed.LeaseId);
                Assert.Null(failed.LeaseExpiresOnUtc);
                Assert.NotNull(failed.NextAttemptOnUtc);
                Assert.Equal("publish_failed", failed.LastError);
                Assert.True(failed.AttemptCount >= 1);

                Assert.Null(await channel.BasicGetAsync(
                    queue: queueName,
                    autoAck: true,
                    cancellationToken: token));

                await channel.QueueBindAsync(
                    queue: queueName,
                    exchange: exchangeName,
                    routingKey: "catalog.product.created.v1",
                    cancellationToken: token);
            }

            await WaitForCompletionAsync(
                adminDb,
                seeded[0].Id,
                seeded[2].Id,
                token);
        }
        finally
        {
            using var shutdown =
                new CancellationTokenSource(TimeSpan.FromSeconds(20));

            await host.StopAsync(shutdown.Token);
        }

        OutboxMessage[] persisted = await adminDb.OutboxMessages
            .AsNoTracking()
            .ToArrayAsync(token);

        foreach (OutboxMessage message in persisted)
        {
            Assert.Null(message.LeaseId);
            Assert.Null(message.LeaseExpiresOnUtc);
            Assert.Null(message.DeadLetteredOnUtc);

            if (message.Id == seeded[1].Id)
            {
                Assert.Null(message.ProcessedOnUtc);
                Assert.Equal(0, message.AttemptCount);
            }
            else
            {
                Assert.NotNull(message.ProcessedOnUtc);
                Assert.Null(message.LastError);
                Assert.Null(message.NextAttemptOnUtc);

                if (!delayedBinding)
                {
                    Assert.Equal(1, message.AttemptCount);
                }
                else
                {
                    int minimumAttempts = message.Id == seeded[0].Id ? 2 : 1;
                    Assert.InRange(message.AttemptCount, minimumAttempts, 5);
                }
            }
        }

        var expected = new Dictionary<Guid, TenantId>
        {
            [seeded[0].Id] = seeded[0].TenantId,
            [seeded[2].Id] = seeded[2].TenantId
        };

        for (int index = 0; index < 2; index++)
        {
            BasicGetResult? received = await channel.BasicGetAsync(
                queue: queueName,
                autoAck: true,
                cancellationToken: token);

            Assert.NotNull(received);

            using JsonDocument document = JsonDocument.Parse(received.Body);
            JsonElement body = document.RootElement;
            Guid messageId = body.GetProperty("messageId").GetGuid();

            Assert.True(expected.Remove(messageId, out TenantId tenantId));
            Assert.Equal(tenantId.Value, body.GetProperty("tenantId").GetGuid());
            Assert.Equal(messageId.ToString("D"), received.BasicProperties.MessageId);

            Assert.NotNull(received.BasicProperties.Headers);

            byte[] header = Assert.IsType<byte[]>(
                received.BasicProperties.Headers["tenant-id"]);

            Assert.Equal(
                tenantId.Value.ToString("D"),
                Encoding.UTF8.GetString(header));
        }

        Assert.Empty(expected);

        Assert.Null(await channel.BasicGetAsync(
            queue: queueName,
            autoAck: true,
            cancellationToken: token));
    }

    private static async Task<OutboxMessage[]> SeedAsync(
        CommerceCoreDbContext db,
        CancellationToken token)
    {
        // Qeyri-aktiv tenant sıralamada iki aktiv tenantın arasındadır.
        TenantId[] ids =
        [
            TenantId.From(Guid.Parse("00000000-0000-0000-0000-000000000001")),
            TenantId.From(Guid.Parse("00000000-0000-0000-0000-000000000002")),
            TenantId.From(Guid.Parse("00000000-0000-0000-0000-000000000003"))
        ];

        var messages = new OutboxMessage[ids.Length];

        for (int index = 0; index < ids.Length; index++)
        {
            Tenant tenant = Tenant.Create(
                ids[index],
                $"e2e-{ids[index].Value:N}",
                "E2E Tenant");

            if (index == 1)
                tenant.Deactivate();

            db.Tenants.Add(tenant);

            messages[index] = OutboxMessage.Create(
                new ProductCreatedDomainEvent(
                    ProductId.New(),
                    DateTimeOffset.UtcNow),
                ids[index],
                JsonOptions);

            db.OutboxMessages.Add(messages[index]);
        }

        await db.SaveChangesAsync(token);

        return messages;
    }

    private static async Task<string> CreateRuntimeRoleAsync(
        CommerceCoreDbContext adminDb,
        string adminConnection,
        CancellationToken token)
    {
        string provisioningSql = await File.ReadAllTextAsync(
            Path.Combine(AppContext.BaseDirectory, "provision-outbox-role.sql"),
            token);

        await adminDb.Database.ExecuteSqlRawAsync(provisioningSql, token);

        await adminDb.Database.ExecuteSqlRawAsync(
            """
            ALTER ROLE commercecore_outbox PASSWORD 'DisposableWorkerPassword!';
            """,
            token);

        // Provisioning is repeatable and must preserve existing credentials.
        await adminDb.Database.ExecuteSqlRawAsync(provisioningSql, token);

        string runtimeConnection = new NpgsqlConnectionStringBuilder(adminConnection)
        {
            Username = "commercecore_outbox",
            Password = "DisposableWorkerPassword!"
        }.ConnectionString;

        await using var connection = new NpgsqlConnection(runtimeConnection);
        await connection.OpenAsync(token);
        await using var privileges = connection.CreateCommand();
        privileges.CommandText = """
            SELECT current_user = 'commercecore_outbox'
                AND NOT (rolsuper OR rolcreatedb OR rolcreaterole OR rolinherit
                         OR rolreplication OR rolbypassrls)
                AND has_table_privilege(current_user, 'platform.tenants', 'SELECT')
                AND NOT has_table_privilege(current_user, 'platform.tenants', 'INSERT, UPDATE, DELETE')
                AND NOT has_table_privilege(current_user, 'platform.tenant_memberships', 'SELECT')
                AND has_table_privilege(current_user, 'outbox.messages', 'SELECT')
                AND has_table_privilege(current_user, 'outbox.messages', 'UPDATE')
                AND NOT has_table_privilege(current_user, 'outbox.messages', 'INSERT, DELETE, TRUNCATE')
                AND NOT has_schema_privilege(current_user, 'catalog', 'USAGE')
            FROM pg_roles WHERE rolname = current_user;
            """;
        Assert.Equal(true, await privileges.ExecuteScalarAsync(token));

        return runtimeConnection;
    }

    private static async Task WaitForCompletionAsync(
        CommerceCoreDbContext db,
        Guid firstId,
        Guid secondId,
        CancellationToken cancellationToken)
    {
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        while (true)
        {
            int completed = await db.OutboxMessages.CountAsync(
                message =>
                    (message.Id == firstId || message.Id == secondId) &&
                    message.ProcessedOnUtc != null,
                timeout.Token);

            if (completed == 2)
                return;

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }

    private static async Task<OutboxMessage> WaitForRetryAsync(
        CommerceCoreDbContext db,
        Guid messageId,
        CancellationToken cancellationToken)
    {
        using var timeout =
            CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeout.CancelAfter(TimeSpan.FromSeconds(45));

        while (true)
        {
            OutboxMessage message = await db.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    candidate => candidate.Id == messageId,
                    timeout.Token);

            if (message.ProcessedOnUtc is not null ||
                message.DeadLetteredOnUtc is not null)
            {
                throw new InvalidOperationException(
                    "Expected a pending retry before the queue was bound.");
            }

            if (message.LastError == "publish_failed" &&
                message.NextAttemptOnUtc is not null)
            {
                return message;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(100), timeout.Token);
        }
    }
}
