using System.Security.Claims;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;

namespace CommerceCore.Platform.Identity;

public sealed class TenantMembershipMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        IPlatformTenantStore tenantStore,
        ITenantContext tenantContext)
    {
        if (!context.Request.Path.StartsWithSegments(
                "/api",
                StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        if (!tenantContext.IsResolved ||
            tenantContext.TenantId is not TenantId tenantId ||
            tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "Tenant resolution must run before tenant membership validation.");
        }

        if (IsPublicStorefrontRead(context))
        {
            await next(context);
            return;
        }

        if (context.User.Identity?.IsAuthenticated != true)
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return;
        }

        string? userSubject =
            context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value ??
            context.User.FindFirst("sub")?.Value;

        if (string.IsNullOrWhiteSpace(userSubject))
        {
            context.Response.StatusCode =
                StatusCodes.Status401Unauthorized;

            return;
        }

        var membership = await tenantStore.GetActiveMembershipAsync(
            tenantId,
            userSubject,
            context.RequestAborted);

        if (membership is null)
        {
            context.Response.StatusCode =
                StatusCodes.Status403Forbidden;

            await context.Response.WriteAsJsonAsync(
                new
                {
                    Title = "Tenant Membership Forbidden",
                    Status = StatusCodes.Status403Forbidden,
                    Detail =
                        "Authenticated user has no active membership in this tenant."
                },
                cancellationToken: context.RequestAborted);

            return;
        }

        await next(context);
    }

    private static bool IsPublicStorefrontRead(HttpContext context)
    {
        Endpoint? endpoint = context.GetEndpoint();

        if (endpoint?.Metadata.GetMetadata<PublicStorefrontReadMetadata>() is null)
        {
            return false;
        }

        bool isReadRequest = HttpMethods.IsGet(context.Request.Method) ||
                             HttpMethods.IsHead(context.Request.Method);
        bool allowsAnonymous = endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null;
        bool hasAuthorizationRequirements = endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>().Count > 0;

        if (!isReadRequest || !allowsAnonymous || hasAuthorizationRequirements)
        {
            throw new InvalidOperationException(
                "Public storefront endpoints must be anonymous GET/HEAD endpoints " +
                "without additional authorization requirements.");
        }

        return true;
    }
}
