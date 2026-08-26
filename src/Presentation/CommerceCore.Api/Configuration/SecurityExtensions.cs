using CommerceCore.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;

namespace CommerceCore.Api.Configuration;

internal static class SecurityExtensions
{
    public static void AddSecurity(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        AuthorizationPolicy fallbackPolicy =
            new AuthorizationPolicyBuilder(
                    JwtBearerDefaults.AuthenticationScheme)
                .RequireAuthenticatedUser()
                .Build();

        builder.Services.AddAuthorizationBuilder()
            .SetFallbackPolicy(fallbackPolicy)
            .AddPolicy(
                AuthorizationPolicies.CatalogRead,
                policy => policy
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasScope(context, "catalog.read")))
            .AddPolicy(
                AuthorizationPolicies.CatalogManage,
                policy => policy
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasScope(context, "catalog.manage")))
            .AddPolicy(
                AuthorizationPolicies.CatalogSchemaManage,
                policy => policy
                    .AddAuthenticationSchemes(
                        JwtBearerDefaults.AuthenticationScheme)
                    .RequireAuthenticatedUser()
                    .RequireAssertion(context =>
                        HasScope(
                            context,
                            "catalog.schema.manage")));
    }

    private static bool HasScope(
        AuthorizationHandlerContext context,
        string requiredScope)
    {
        return context.User
            .FindAll("scope")
            .SelectMany(claim => claim.Value.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries))
            .Contains(requiredScope, StringComparer.Ordinal);
    }
}
