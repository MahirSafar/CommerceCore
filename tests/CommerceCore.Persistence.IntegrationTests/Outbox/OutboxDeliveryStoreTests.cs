using System.Text.Json;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.Outbox;

[Collection(nameof(PostgreSqlCollection))]
public sealed class OutboxDeliveryStoreTests(PostgreSqlFixture fixture)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Lease_ProtectsConcurrentClaimsAndRejectsPreviousOwner()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
        TenantId otherTenantId =
            await fixture.CreateTenantAsync(cancellationToken);

        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        try
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();

            Guid messageId = Guid.NewGuid();

            db.OutboxMessages.Add(OutboxMessage.Create(
                new TestEvent(messageId, DateTimeOffset.UtcNow),
                tenantId,
                JsonOptions));

            await db.SaveChangesAsync(cancellationToken);

            LeasedOutboxMessage?[] claims = await Task.WhenAll(
                ClaimAsync(cancellationToken),
                ClaimAsync(cancellationToken));

            LeasedOutboxMessage first = Assert.Single(
                claims.OfType<LeasedOutboxMessage>());

            Assert.Equal(messageId, first.Id);
            Assert.Equal(1, first.AttemptCount);

            tenantContext.SetTenant(otherTenantId);

            Assert.Null(await ClaimAsync(cancellationToken));

            Assert.False(await CompleteAsync(
                messageId,
                first.LeaseId,
                cancellationToken));

            Assert.False(await FailAsync(
                messageId,
                first.LeaseId,
                cancellationToken));

            tenantContext.SetTenant(tenantId);

            Assert.Null(await ClaimAsync(cancellationToken));

            Assert.False(await CompleteAsync(
                messageId,
                Guid.NewGuid(),
                cancellationToken));

            // Vaxt gözləmədən lease-in bitməsini simulyasiya edir.
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE outbox.messages
                SET lease_expires_on_utc = clock_timestamp() - interval '1 second'
                WHERE id = {messageId}
                """,
                cancellationToken);

            Assert.False(await CompleteAsync(
                messageId,
                first.LeaseId,
                cancellationToken));

            LeasedOutboxMessage? second = await ClaimAsync(cancellationToken);
            Assert.NotNull(second);
            Assert.Equal(messageId, second.Id);
            Assert.NotEqual(first.LeaseId, second.LeaseId);
            Assert.Equal(2, second.AttemptCount);

            Assert.False(await CompleteAsync(
                messageId,
                first.LeaseId,
                cancellationToken));

            Assert.True(await CompleteAsync(
                messageId,
                second.LeaseId,
                cancellationToken));

            Assert.Null(await ClaimAsync(cancellationToken));

            var persisted = await db.OutboxMessages
                .AsNoTracking()
                .SingleAsync(
                    message => message.Id == messageId,
                    cancellationToken);

            Assert.NotNull(persisted.ProcessedOnUtc);
            Assert.Null(persisted.LeaseId);
            Assert.Null(persisted.LeaseExpiresOnUtc);
            Assert.Null(persisted.NextAttemptOnUtc);
            Assert.Equal(2, persisted.AttemptCount);
        }
        finally
        {
            tenantContext.Clear();
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retry_StopsAfterMaximumAttempts(
        bool crashOnLastAttempt)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId =
            await fixture.CreateTenantAsync(cancellationToken);

        var tenantContext =
            fixture.Services.GetRequiredService<TestTenantContext>();

        tenantContext.SetTenant(tenantId);

        try
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider
                .GetRequiredService<CommerceCoreDbContext>();

            Guid messageId = Guid.NewGuid();

            db.OutboxMessages.Add(OutboxMessage.Create(
                new TestEvent(messageId, DateTimeOffset.UtcNow),
                tenantId,
                JsonOptions));

            await db.SaveChangesAsync(cancellationToken);

            for (int attempt = 1; attempt <= 5; attempt++)
            {
                var claimed = await ClaimAsync(cancellationToken);

                Assert.NotNull(claimed);
                Assert.Equal(messageId, claimed.Id);
                Assert.Equal(attempt, claimed.AttemptCount);

                if (crashOnLastAttempt && attempt == 5)
                {
                    // Aktiv son lease vaxtından əvvəl dead edilməməlidir.
                    Assert.Null(await ClaimAsync(cancellationToken));

                    var active = await db.OutboxMessages
                        .AsNoTracking()
                        .SingleAsync(m => m.Id == messageId, cancellationToken);

                    Assert.Null(active.DeadLetteredOnUtc);

                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE outbox.messages
                        SET lease_expires_on_utc =
                            clock_timestamp() - interval '1 second'
                        WHERE id = {messageId}
                        """,
                        cancellationToken);

                    Assert.False(await FailAsync(
                        messageId,
                        claimed.LeaseId,
                        cancellationToken));

                    break;
                }

                Assert.True(await FailAsync(
                    messageId,
                    claimed.LeaseId,
                    cancellationToken));

                // İstifadə olunmuş lease artıq keçərli deyil.
                Assert.False(await FailAsync(
                    messageId,
                    claimed.LeaseId,
                    cancellationToken));

                var failed = await db.OutboxMessages
                    .AsNoTracking()
                    .SingleAsync(m => m.Id == messageId, cancellationToken);

                Assert.Null(failed.LeaseId);
                Assert.Null(failed.LeaseExpiresOnUtc);

                if (attempt < 5)
                {
                    Assert.NotNull(failed.NextAttemptOnUtc);
                    Assert.Null(failed.DeadLetteredOnUtc);

                    // Retry vaxtına hələ çatılmayıb.
                    Assert.Null(await ClaimAsync(cancellationToken));

                    // Testdə real vaxt gözləmirik.
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE outbox.messages
                        SET next_attempt_on_utc =
                            clock_timestamp() - interval '1 second'
                        WHERE id = {messageId}
                        """,
                        cancellationToken);
                }
            }

            Assert.Null(await ClaimAsync(cancellationToken));

            var terminal = await db.OutboxMessages
                .AsNoTracking()
                .SingleAsync(m => m.Id == messageId, cancellationToken);

            Assert.Equal(5, terminal.AttemptCount);
            Assert.NotNull(terminal.DeadLetteredOnUtc);
            Assert.Null(terminal.ProcessedOnUtc);
            Assert.Null(terminal.NextAttemptOnUtc);
            Assert.Null(terminal.LeaseId);
            Assert.Null(terminal.LeaseExpiresOnUtc);
        }
        finally
        {
            tenantContext.Clear();
        }
    }

    private async Task<LeasedOutboxMessage?> ClaimAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var store = new OutboxDeliveryStore(
            scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantContext>());

        return await store.ClaimAsync(
            TimeSpan.FromMinutes(5),
            cancellationToken);
    }

    private async Task<bool> CompleteAsync(
        Guid messageId,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var store = new OutboxDeliveryStore(
            scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantContext>());

        return await store.CompleteAsync(
            messageId,
            leaseId,
            cancellationToken);
    }

    private async Task<bool> FailAsync(
        Guid messageId,
        Guid leaseId,
        CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();

        var store = new OutboxDeliveryStore(
            scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>(),
            scope.ServiceProvider.GetRequiredService<ITenantContext>());

        return await store.FailAsync(
            messageId,
            leaseId,
            "broker_unavailable",
            cancellationToken);
    }

    private sealed record TestEvent(
        Guid EventId,
        DateTimeOffset OccurredOnUtc) : IDomainEvent;
}
