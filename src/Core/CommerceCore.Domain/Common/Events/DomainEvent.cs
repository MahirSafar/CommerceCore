namespace CommerceCore.Domain.Common.Events;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent(DateTimeOffset occurredOnUtc)
      : this(Guid.CreateVersion7(), occurredOnUtc)
    {
    }

    protected DomainEvent(Guid eventId, DateTimeOffset occurredOnUtc)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
          eventId,
          Guid.Empty);

        ArgumentOutOfRangeException.ThrowIfEqual(
          occurredOnUtc,
          DateTimeOffset.MinValue);

        EventId = eventId;
        OccurredOnUtc = occurredOnUtc.ToUniversalTime();
    }

    public Guid EventId { get; }

    public DateTimeOffset OccurredOnUtc { get; }
}

