using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Events;

namespace CommerceCore.Domain.Catalog.Products.Events;

public sealed record ProductArchivedDomainEvent : DomainEvent
{
    public ProductArchivedDomainEvent(
        ProductId productId,
        DateTimeOffset archivedAtUtc,
        string? archivedBy)
        : base(archivedAtUtc)
    {
        if (productId == default)
        {
            throw new ArgumentException(
                "Product ID cannot be empty.",
                nameof(productId));
        }

        ProductId = productId;
        ArchivedAtUtc = archivedAtUtc.ToUniversalTime();
        ArchivedBy = archivedBy;
    }

    public ProductId ProductId { get; }

    public DateTimeOffset ArchivedAtUtc { get; }

    public string? ArchivedBy { get; }
}