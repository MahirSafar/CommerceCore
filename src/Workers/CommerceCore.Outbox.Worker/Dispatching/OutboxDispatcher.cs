using System.Text.Json;
using CommerceCore.Outbox.Worker.Messaging;
using CommerceCore.Persistence.Outbox;

namespace CommerceCore.Outbox.Worker.Dispatching;

public enum OutboxDispatchResult
{
    Empty,
    Completed,
    FailureRecorded,
    LeaseLost
}

public sealed partial class OutboxDispatcher(
    IOutboxDeliveryStore store,
    IEventPublisher publisher,
    ILogger<OutboxDispatcher> logger)
{
    private static readonly TimeSpan LeaseDuration =
        TimeSpan.FromMinutes(1);

    private readonly IOutboxDeliveryStore _store = store;
    private readonly IEventPublisher _publisher = publisher;
    private readonly ILogger<OutboxDispatcher> _logger = logger;

    public async Task<OutboxDispatchResult> DispatchOnceAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        LeasedOutboxMessage? message = await _store.ClaimAsync(
            LeaseDuration,
            cancellationToken);

        if (message is null)
        {
            return OutboxDispatchResult.Empty;
        }

        cancellationToken.ThrowIfCancellationRequested();

        OutboundEvent outbound;

        try
        {
            outbound = OutboxEventMapper.Map(message);
        }
        catch (JsonException)
        {
            return await RecordFailureAsync(
                message,
                "invalid_event_payload",
                cancellationToken);
        }
        catch (NotSupportedException)
        {
            return await RecordFailureAsync(
                message,
                "unsupported_event_type",
                cancellationToken);
        }

        try
        {
            await _publisher.PublishAsync(outbound, cancellationToken);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Dayandırılma zamanı lease öz vaxtında bitəcək.
            throw;
        }
        catch (Exception exception)
        {
            LogPublishFailed(
                _logger,
                message.Id,
                message.TenantId.Value,
                exception.GetType().Name);

            string errorCode = exception is OperationCanceledException
                ? "publish_timeout"
                : "publish_failed";

            return await RecordFailureAsync(
                message,
                errorCode,
                cancellationToken);
        }

        // Completion xətası publish xətası kimi qeydə alınmamalıdır.
        bool completed = await _store.CompleteAsync(
            message.Id,
            message.LeaseId,
            cancellationToken);

        return completed
            ? OutboxDispatchResult.Completed
            : OutboxDispatchResult.LeaseLost;
    }

    private async Task<OutboxDispatchResult> RecordFailureAsync(
        LeasedOutboxMessage message,
        string errorCode,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        bool recorded = await _store.FailAsync(
            message.Id,
            message.LeaseId,
            errorCode,
            cancellationToken);

        return recorded
            ? OutboxDispatchResult.FailureRecorded
            : OutboxDispatchResult.LeaseLost;
    }

    [LoggerMessage(
        EventId = 10,
        Level = LogLevel.Warning,
        Message =
            "Outbox publish failed for message {MessageId}, tenant {TenantId}: {ErrorType}.")]
    private static partial void LogPublishFailed(
        ILogger logger,
        Guid messageId,
        Guid tenantId,
        string errorType);
}
