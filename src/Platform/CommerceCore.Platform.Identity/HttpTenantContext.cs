using CommerceCore.Platform.Contracts;
using Microsoft.AspNetCore.Http;

namespace CommerceCore.Platform.Identity;

public sealed class HttpTenantContext(IHttpContextAccessor httpContextAccessor) : ITenantContext
{
    private const string ItemKey = "__TenantContext";
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private ITenantContext CurrentContext
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext != null &&
                httpContext.Items.TryGetValue(ItemKey, out var item) &&
                item is ITenantContext tenantContext)
            {
                return tenantContext;
            }

            return TenantContext.Empty;
        }
    }

    public TenantId? TenantId => CurrentContext.TenantId;
    public StorefrontId? StorefrontId => CurrentContext.StorefrontId;
    public MarketId? MarketId => CurrentContext.MarketId;
    public string? DefaultLocale => CurrentContext.DefaultLocale;
    public bool IsResolved => CurrentContext.IsResolved;

    public static void SetContext(HttpContext httpContext, ITenantContext context)
    {
        httpContext.Items[ItemKey] = context;
    }
}
