using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Events;

namespace CommerceCore.Domain.Catalog.Products.Events;

public sealed record ProductArchivedDomainEvent(
    ProductId ProductId,
    DateTimeOffset ArchivedAtUtc,
    string? ArchivedBy)
    : DomainEvent(ArchivedAtUtc);