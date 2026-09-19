namespace CommerceCore.Outbox.Worker.Messaging;

public sealed record OutboundEvent(
    Guid MessageId,
    Guid TenantId,
    string EventType,
    ReadOnlyMemory<byte> Body);
