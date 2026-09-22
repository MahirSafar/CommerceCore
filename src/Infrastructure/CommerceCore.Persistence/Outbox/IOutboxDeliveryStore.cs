namespace CommerceCore.Persistence.Outbox;

public interface IOutboxDeliveryStore
{
    Task<LeasedOutboxMessage?> ClaimAsync(
        TimeSpan leaseDuration,
        CancellationToken cancellationToken);

    Task<bool> CompleteAsync(
        Guid messageId,
        Guid leaseId,
        CancellationToken cancellationToken);

    Task<bool> FailAsync(
        Guid messageId,
        Guid leaseId,
        string errorCode,
        CancellationToken cancellationToken);
}
