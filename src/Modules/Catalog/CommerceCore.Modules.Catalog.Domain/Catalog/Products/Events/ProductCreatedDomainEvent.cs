using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Events;

namespace CommerceCore.Domain.Catalog.Products.Events;

public sealed record ProductCreatedDomainEvent : DomainEvent
{
    public ProductCreatedDomainEvent(
        ProductId productId,
        DateTimeOffset occurredOnUtc)
        : base(occurredOnUtc)
    {
        if (productId == default)
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));

        ProductId = productId;
    }
    public ProductId ProductId { get; }

}
