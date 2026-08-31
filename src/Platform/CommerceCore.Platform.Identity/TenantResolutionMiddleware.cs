using System.Security.Claims;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using Microsoft.AspNetCore.Http;

namespace CommerceCore.Platform.Identity;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context, IPlatformTenantStore tenantStore)
    {
        var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;

        // Skip non-API or public discovery endpoints (like swagger, health, scalar)
        if (!path.StartsWith("/api/"))
        {
            await _next(context);
            return;
        }

        if (!await TryResolveTenantAsync(context, tenantStore))
        {
            return;
        }

        await _next(context);
    }

    private static async Task<bool> TryResolveTenantAsync(
        HttpContext context,
        IPlatformTenantStore tenantStore)
    {
        string host = context.Request.Host.Host;

        var storefront = await tenantStore.GetStorefrontByHostAsync(
            host,
            context.RequestAborted);

        if (storefront is null || !storefront.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(new
            {
                Title = "Invalid Storefront",
                Status = StatusCodes.Status400BadRequest,
                Detail = $"Storefront for host '{host}' was not found or is inactive."
            });

            return false;
        }

        string? userSubject =
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userSubject))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return false;
        }

        var membership = await tenantStore.GetActiveMembershipAsync(
            storefront.TenantId,
            userSubject,
            context.RequestAborted);

        if (membership is null)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(new
            {
                Title = "Tenant Membership Forbidden",
                Status = StatusCodes.Status403Forbidden,
                Detail = "Authenticated user has no active membership in this tenant."
            });

            return false;
        }

        var tenantContext = TenantContext.ForTenant(
            storefront.TenantId,
            storefront.StorefrontId,
            storefront.MarketId,
            storefront.DefaultLocale);

        HttpTenantContext.SetContext(context, tenantContext);

        return true;
    }
}
