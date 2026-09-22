using CommerceCore.Platform.Contracts;

namespace CommerceCore.Outbox.Worker.Dispatching;

public sealed class WorkerTenantContext : ITenantContext
{
    public TenantId? TenantId { get; private set; }
    public StorefrontId? StorefrontId => null;
    public MarketId? MarketId => null;
    public string? DefaultLocale => null;
    public bool IsResolved => TenantId.HasValue;

    public void Initialize(TenantId tenantId)
    {
        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        }

        if (IsResolved)
        {
            throw new InvalidOperationException(
                "A worker scope cannot change its tenant.");
        }

        TenantId = tenantId;
    }
}
