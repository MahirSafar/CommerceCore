using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class Storefront
{
    private Storefront()
    {
    }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public string HostName { get; private set; } = string.Empty;
    public string MarketCode { get; private set; } = "AZ";
    public string DefaultLocale { get; private set; } = "az-AZ";
    public bool IsActive { get; private set; } = true;

    public StorefrontId StorefrontId => StorefrontId.From(Id);
    public MarketId MarketId => MarketId.From(MarketCode);

    public static Storefront Create(
        StorefrontId storefrontId,
        TenantId tenantId,
        string hostName,
        MarketId marketId,
        string defaultLocale = "az-AZ")
    {
        if (storefrontId.Value == Guid.Empty)
        {
            throw new ArgumentException("Storefront ID cannot be empty.", nameof(storefrontId));
        }

        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(marketId.Code))
        {
            throw new ArgumentException("Market ID cannot be empty.", nameof(marketId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(hostName);
        ArgumentException.ThrowIfNullOrWhiteSpace(defaultLocale);

        string normalizedHost = hostName.Trim().TrimEnd('.').ToLowerInvariant();

        if (Uri.CheckHostName(normalizedHost) == UriHostNameType.Unknown)
        {
            throw new ArgumentException("Host name is invalid.", nameof(hostName));
        }

        return new Storefront
        {
            Id = storefrontId.Value,
            TenantId = tenantId,
            HostName = normalizedHost,
            MarketCode = marketId.Code,
            DefaultLocale = defaultLocale.Trim(),
            IsActive = true
        };
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
