using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class Storefront
{
    public Guid Id { get; set; }
    public TenantId TenantId { get; set; }
    public string HostName { get; set; } = string.Empty;
    public string MarketCode { get; set; } = "AZ";
    public string DefaultLocale { get; set; } = "az-AZ";
    public bool IsActive { get; set; } = true;

    public StorefrontId StorefrontId => StorefrontId.From(Id);
    public MarketId MarketId => MarketId.From(MarketCode);
}
