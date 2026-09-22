using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.RabbitMq;
using CommerceCore.Persistence;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CommerceCore.Outbox.Worker.Dispatching;

public sealed partial class OutboxWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> options,
    ILogger<OutboxWorker> logger) : BackgroundService
{
    private const int TenantPageSize = 100;
    private const int MessagesPerTenant = 10;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan FailureDelay = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<OutboxWorker> _logger = logger;
    private Guid _lastTenantId;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.DispatcherEnabled)
        {
            LogDisabled(_logger);
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using RabbitMqEventPublisher publisher = await CreatePublisherAsync(stoppingToken);
                await RunSessionAsync(publisher, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                LogSessionFailed(_logger, exception.GetType().Name);
            }

            try
            {
                await Task.Delay(FailureDelay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    private async Task<RabbitMqEventPublisher> CreatePublisherAsync(
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(20));
        return await RabbitMqEventPublisher.CreateAsync(
            _options,
            timeout.Token);
    }

    private async Task RunSessionAsync(
        RabbitMqEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            Guid[] tenantIds = await ReadTenantPageAsync(cancellationToken);

            if (tenantIds.Length == 0)
            {
                _lastTenantId = Guid.Empty;
                await Task.Delay(IdleDelay, cancellationToken);
                continue;
            }

            foreach (Guid tenantId in tenantIds)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Bir tenant-dakı xəta digərlərinin növbəsini bağlamasın.
                _lastTenantId = tenantId;

                OutboxDispatchResult result = await DispatchTenantAsync(
                    TenantId.From(tenantId),
                    publisher,
                    cancellationToken);

                if (result is OutboxDispatchResult.FailureRecorded or OutboxDispatchResult.LeaseLost)
                {
                    LogTenantInterrupted(_logger, tenantId, result);
                    return;
                }
            }
        }
    }

    private async Task<Guid[]> ReadTenantPageAsync(
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformReadDbContext>();

        return await db.Database.SqlQuery<Guid>(
            $"""
            SELECT id AS "Value"
            FROM platform.tenants
            WHERE status = 'Active' AND id > {_lastTenantId}
            ORDER BY id
            LIMIT {TenantPageSize}
            """)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<OutboxDispatchResult> DispatchTenantAsync(
        TenantId tenantId,
        RabbitMqEventPublisher publisher,
        CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var platformDb = scope.ServiceProvider.GetRequiredService<PlatformReadDbContext>();

        bool active = await platformDb.Tenants.AnyAsync(
            tenant => tenant.Id == tenantId && tenant.Status == TenantStatuses.Active,
            cancellationToken);

        if (!active)
        {
            return OutboxDispatchResult.Empty;
        }

        scope.ServiceProvider
            .GetRequiredService<WorkerTenantContext>()
            .Initialize(tenantId);

        var dispatcher = new OutboxDispatcher(
            scope.ServiceProvider.GetRequiredService<IOutboxDeliveryStore>(),
            publisher,
            scope.ServiceProvider.GetRequiredService<ILogger<OutboxDispatcher>>());

        for (int index = 0; index < MessagesPerTenant; index++)
        {
            OutboxDispatchResult result = await dispatcher.DispatchOnceAsync(cancellationToken);

            if (result != OutboxDispatchResult.Completed)
            {
                return result;
            }
        }

        return OutboxDispatchResult.Completed;
    }

    [LoggerMessage(
        EventId = 20,
        Level = LogLevel.Information,
        Message = "Outbox dispatcher is disabled.")]
    private static partial void LogDisabled(ILogger logger);

    [LoggerMessage(
        EventId = 21,
        Level = LogLevel.Error,
        Message = "Outbox worker session failed: {ErrorType}.")]
    private static partial void LogSessionFailed(
        ILogger logger,
        string errorType);

    [LoggerMessage(
        EventId = 22,
        Level = LogLevel.Warning,
        Message = "Outbox tenant {TenantId} returned {Result}; reconnecting after delay.")]
    private static partial void LogTenantInterrupted(
        ILogger logger,
        Guid tenantId,
        OutboxDispatchResult result);
}
