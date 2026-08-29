namespace CommerceCore.Platform.Contracts;

public interface ITenantContext
{
    TenantId? TenantId { get; }
    StorefrontId? StorefrontId { get; }
    MarketId? MarketId { get; }
    string? DefaultLocale { get; }
    bool IsResolved { get; }
}
