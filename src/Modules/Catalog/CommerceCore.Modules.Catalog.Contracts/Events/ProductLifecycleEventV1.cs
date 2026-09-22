namespace CommerceCore.Modules.Catalog.Contracts.Events;

public sealed record ProductLifecycleEventV1(
    Guid MessageId,
    Guid TenantId,
    string Type,
    DateTimeOffset OccurredOnUtc,
    Guid ProductId);
