namespace CommerceCore.Outbox.Worker.Messaging;

public interface IEventPublisher
{
    Task PublishAsync(
        OutboundEvent message,
        CancellationToken cancellationToken);
}
