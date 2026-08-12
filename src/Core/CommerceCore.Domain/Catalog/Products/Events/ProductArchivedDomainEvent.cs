using CommerceCore.Domain.Common.Events;

namespace CommerceCore.Domain.Catalog.Products.Events;

public sealed record ProductArchivedDomainEvent : DomainEvent
{
    public ProductId ProductId { get; }
    public ProductArchivedDomainEvent(ProductId productId, DateTimeOffset occurredOnUtc)
            : base(occurredOnUtc)
    {
        ProductId = productId;
    }
}
