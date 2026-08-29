namespace CommerceCore.Platform.Contracts;

public sealed record TenantContext(
    TenantId? TenantId,
    StorefrontId? StorefrontId = null,
    MarketId? MarketId = null,
    string? DefaultLocale = null) : ITenantContext
{
    public bool IsResolved => TenantId.HasValue;

    public static TenantContext Empty => new(null, null, null, null);

    public static TenantContext ForTenant(TenantId tenantId, StorefrontId? storefrontId = null, MarketId? marketId = null, string? defaultLocale = null)
        => new(tenantId, storefrontId, marketId, defaultLocale);
}
