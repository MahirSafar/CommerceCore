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

    public static IApplicationBuilder UsePlatformTenantResolution(this IApplicationBuilder app)
    {
        return app.UseMiddleware<TenantResolutionMiddleware>();
    }
}
