using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using Microsoft.AspNetCore.Http;

namespace CommerceCore.Platform.Identity;

public sealed class TenantResolutionMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPlatformTenantStore tenantStore)
    {
        if (!context.Request.Path.StartsWithSegments(
                "/api",
                StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        string host = context.Request.Host.Host;

        var storefront = await tenantStore.GetStorefrontByHostAsync(
            host,
            context.RequestAborted);

        if (storefront is null || !storefront.IsActive)
        {
            context.Response.StatusCode =
                StatusCodes.Status400BadRequest;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    Title = "Invalid Storefront",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "Storefront was not found or is inactive."
                },
                cancellationToken: context.RequestAborted);

            return;
        }

        HttpTenantContext.SetContext(
            context,
            TenantContext.ForTenant(
                storefront.TenantId,
                storefront.StorefrontId,
                storefront.MarketId,
                storefront.DefaultLocale));

        await next(context);
    }
}
