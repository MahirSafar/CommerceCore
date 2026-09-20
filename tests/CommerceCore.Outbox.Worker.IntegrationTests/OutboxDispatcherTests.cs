using System.Text.Json;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Outbox.Worker.Dispatching;
using CommerceCore.Outbox.Worker.Messaging;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class OutboxDispatcherTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IOutboxDeliveryStore _store =
        Substitute.For<IOutboxDeliveryStore>();

    private readonly IEventPublisher _publisher =
        Substitute.For<IEventPublisher>();

    private readonly LeasedOutboxMessage _message = CreateMessage();

    private readonly OutboxDispatcher _dispatcher;

    public OutboxDispatcherTests()
    {
        _store.ClaimAsync(
                Arg.Any<TimeSpan>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<LeasedOutboxMessage?>(_message));

        _store.CompleteAsync(
                _message.Id,
                _message.LeaseId,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _store.FailAsync(
                _message.Id,
                _message.LeaseId,
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        _dispatcher = new OutboxDispatcher(
            _store,
            _publisher,
            NullLogger<OutboxDispatcher>.Instance);
    }

    [Theory]
    [InlineData(true, OutboxDispatchResult.Completed)]
    [InlineData(false, OutboxDispatchResult.LeaseLost)]
    public async Task Dispatch_WaitsForPublishBeforeCompletion(
        bool completionAccepted,
        OutboxDispatchResult expected)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        var started = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        var confirmation = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _publisher.PublishAsync(
                Arg.Any<OutboundEvent>(),
                token)
            .Returns(_ =>
            {
                started.TrySetResult(true);
                return confirmation.Task;
            });

        _store.CompleteAsync(_message.Id, _message.LeaseId, token)
            .Returns(Task.FromResult(completionAccepted));

        Task<OutboxDispatchResult> dispatch =
            _dispatcher.DispatchOnceAsync(token);

        try
        {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(5), token);

            await _store.DidNotReceive().CompleteAsync(
                Arg.Any<Guid>(),
                Arg.Any<Guid>(),
                Arg.Any<CancellationToken>());
        }
        finally
        {
            confirmation.TrySetResult(true);
        }

        Assert.Equal(expected, await dispatch);

        await _store.Received(1).CompleteAsync(
            _message.Id,
            _message.LeaseId,
            token);

        await _publisher.Received(1).PublishAsync(
            Arg.Is<OutboundEvent>(message =>
                message.MessageId == _message.Id &&
                message.TenantId == _message.TenantId.Value),
            token);
    }

    [Fact]
    public async Task Dispatch_PublishFailure_RecordsFailureWithoutCompletion()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        _publisher.PublishAsync(Arg.Any<OutboundEvent>(), token)
            .Returns(Task.FromException(
                new IOException("Simulated broker failure.")));

        Assert.Equal(
            OutboxDispatchResult.FailureRecorded,
            await _dispatcher.DispatchOnceAsync(token));

        await _store.Received(1).FailAsync(
            _message.Id,
            _message.LeaseId,
            "publish_failed",
            token);

        await _store.DidNotReceive().CompleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_CompletionFailure_DoesNotRecordPublishFailure()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        _publisher.PublishAsync(Arg.Any<OutboundEvent>(), token)
            .Returns(Task.CompletedTask);

        _store.CompleteAsync(_message.Id, _message.LeaseId, token)
            .Returns(Task.FromException<bool>(
                new IOException("Simulated database failure.")));

        await Assert.ThrowsAsync<IOException>(
            () => _dispatcher.DispatchOnceAsync(token));

        await _store.DidNotReceive().FailAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Dispatch_ShutdownDuringPublish_DoesNotRecordFailure()
    {
        using var cancellation =
            CancellationTokenSource.CreateLinkedTokenSource(
                TestContext.Current.CancellationToken);

        CancellationToken token = cancellation.Token;

        _publisher.PublishAsync(Arg.Any<OutboundEvent>(), token)
            .Returns(_ =>
            {
                cancellation.Cancel();
                return Task.FromCanceled(token);
            });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _dispatcher.DispatchOnceAsync(token));

        await _store.DidNotReceive().FailAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _store.DidNotReceive().CompleteAsync(
            Arg.Any<Guid>(),
            Arg.Any<Guid>(),
            Arg.Any<CancellationToken>());
    }

    private static LeasedOutboxMessage CreateMessage()
    {
        var domainEvent = new ProductCreatedDomainEvent(
            ProductId.New(),
            DateTimeOffset.UtcNow);

        OutboxMessage stored = OutboxMessage.Create(
            domainEvent,
            TenantId.New(),
            JsonOptions);

        return new LeasedOutboxMessage(
            stored.Id,
            stored.TenantId,
            Guid.NewGuid(),
            1,
            stored.Type,
            stored.Content);
    }
}
