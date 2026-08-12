using CommerceCore.Domain.Common.Events;
using System.Collections.ObjectModel;

namespace CommerceCore.Domain.Common.Entities;

public abstract class AggregateRoot<TKey>
  : AuditableEntity<TKey>, IHasDomainEvents
  where TKey : notnull
{
    private readonly List<IDomainEvent> _domainEvents = [];
    private readonly ReadOnlyCollection<IDomainEvent> _readOnlyDomainEvents;

    protected AggregateRoot()
    {
        _readOnlyDomainEvents = _domainEvents.AsReadOnly();
    }

    protected AggregateRoot(TKey id)
      : base(id)
    {
        _readOnlyDomainEvents = _domainEvents.AsReadOnly();
    }

    public IReadOnlyCollection<IDomainEvent> DomainEvents
      => _readOnlyDomainEvents;

    protected void RaiseDomainEvent(IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        _domainEvents.Add(domainEvent);
    }

    void IHasDomainEvents.ClearDomainEvents()
      => _domainEvents.Clear();
}
