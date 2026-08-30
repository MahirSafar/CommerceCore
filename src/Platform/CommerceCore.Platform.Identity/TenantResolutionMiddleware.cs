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

        if (path.StartsWith("/api/storefront"))
        {
            var host = context.Request.Host.Host;
            var storefront = await tenantStore.GetStorefrontByHostAsync(host, context.RequestAborted);
            if (storefront is null || !storefront.IsActive)
            {
                context.Response.StatusCode = StatusCodes.Status400BadRequest;
                await context.Response.WriteAsJsonAsync(new
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Invalid Storefront",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = $"Storefront for host '{host}' was not found or is inactive."
                });
                return;
            }

            var tenantContext = TenantContext.ForTenant(
                storefront.TenantId,
                storefront.StorefrontId,
                storefront.MarketId,
                storefront.DefaultLocale);

            HttpTenantContext.SetContext(context, tenantContext);
        }
        else if (path.StartsWith("/api/admin"))
        {
            var host = context.Request.Host.Host;

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

                return;
            }

            var userSub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userSub))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var membership = await tenantStore.GetActiveMembershipAsync(
                storefront.TenantId,
                userSub,
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

                return;
            }

            var tenantContext = TenantContext.ForTenant(
                storefront.TenantId,
                storefront.StorefrontId,
                storefront.MarketId,
                storefront.DefaultLocale);

            HttpTenantContext.SetContext(context, tenantContext);
        }
        else if (path.StartsWith("/api/partner"))
        {
            var clientId = context.User.FindFirst("client_id")?.Value;

            if (string.IsNullOrWhiteSpace(clientId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var tenant = await tenantStore.GetTenantByPartnerClientIdAsync(clientId, context.RequestAborted);
            if (tenant is null || tenant.Status != "Active")
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.3",
                    Title = "Partner Access Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail = $"Partner client '{clientId}' is not authorized for any tenant."
                });
                return;
            }

            var tenantContext = TenantContext.ForTenant(tenant.Id);
            HttpTenantContext.SetContext(context, tenantContext);
        }
        else
        {
            var host = context.Request.Host.Host;

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

                return;
            }

            var userSub = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? context.User.FindFirst("sub")?.Value;

            if (string.IsNullOrWhiteSpace(userSub))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            var membership = await tenantStore.GetActiveMembershipAsync(
                storefront.TenantId,
                userSub,
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

                return;
            }

            var tenantContext = TenantContext.ForTenant(
                storefront.TenantId,
                storefront.StorefrontId,
                storefront.MarketId,
                storefront.DefaultLocale);

            HttpTenantContext.SetContext(context, tenantContext);
        }

        if (!HttpTenantContext.HasResolvedTenant(context))
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            await context.Response.WriteAsJsonAsync(new
            {
                Title = "Tenant Resolution Required",
                Status = StatusCodes.Status400BadRequest,
                Detail = "The request could not be associated with an active tenant."
            });

            return;
        }

        await _next(context);
    }
}
