using CommerceCore.Domain.Common.Events;
using System.Text.Json;

namespace CommerceCore.Persistence.Outbox;

public sealed class OutboxMessage
{
    private OutboxMessage()
    {
    }

    private OutboxMessage(
        Guid id,
        DateTimeOffset occurredOnUtc,
        string type,
        string content)
    {
        Id = id;
        OccurredOnUtc = occurredOnUtc;
        Type = type;
        Content = content;
    }
    public Guid Id { get; private set; }
    public DateTimeOffset OccurredOnUtc { get; private set; }
    public string Type { get; private set; } = null!;
    public string Content { get; private set; } = null!;
    public DateTimeOffset? ProcessedOnUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }

    public static OutboxMessage Create(
        IDomainEvent domainEvent,
        JsonSerializerOptions serializerOptions)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        ArgumentNullException.ThrowIfNull(serializerOptions);

        Type eventType = domainEvent.GetType();

        string content = JsonSerializer.Serialize(domainEvent, eventType, serializerOptions);

        return new OutboxMessage(
            domainEvent.EventId,
            domainEvent.OccurredOnUtc,
            eventType.FullName ?? eventType.Name, content);
    }
}
