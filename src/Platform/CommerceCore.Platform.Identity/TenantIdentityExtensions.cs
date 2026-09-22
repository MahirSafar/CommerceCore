using CommerceCore.Platform.Contracts;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Platform.Identity;

public static class TenantIdentityExtensions
{
    public static IServiceCollection AddPlatformTenantServices(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ITenantContext, HttpTenantContext>();
        return services;
    }

    public static IApplicationBuilder UsePlatformTenancy(
        this IApplicationBuilder app)
    {
        app.UseMiddleware<TenantResolutionMiddleware>();
        app.UseMiddleware<TenantMembershipMiddleware>();

        return app;
    }
}
