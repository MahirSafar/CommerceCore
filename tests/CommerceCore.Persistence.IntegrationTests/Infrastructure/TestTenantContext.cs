using CommerceCore.Platform.Contracts;

namespace CommerceCore.Persistence.IntegrationTests.Infrastructure;

public sealed class TestTenantContext : ITenantContext
{
    private static readonly AsyncLocal<TenantId?> _currentTenant = new();

    public TenantId? TenantId
    {
        get => _currentTenant.Value;
        set => _currentTenant.Value = value;
    }

    public StorefrontId? StorefrontId => null;
    public MarketId? MarketId => null;
    public string? DefaultLocale => null;
    public bool IsResolved => TenantId.HasValue;

    public void SetTenant(TenantId? tenantId)
    {
        _currentTenant.Value = tenantId;
    }

    public void Clear()
    {
        _currentTenant.Value = null;
    }
}
