using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.Interceptors;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<AuditingSaveChangesInterceptor>();

        services.AddDbContext<CommerceCoreDbContext>(
            (serviceProvider, options) =>
            {
                options.UseNpgsql(
                    connectionString,
                    npgsqlOptions =>
                    {
                        npgsqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 5,
                            maxRetryDelay: TimeSpan.FromSeconds(10),
                            errorCodesToAdd: null);
                    });

                options.AddInterceptors(
                    serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>());
            });

        services.AddScoped<ICommerceCoreDbContext>(serviceProvider => serviceProvider.GetRequiredService<CommerceCoreDbContext>());
        return services;
    }
}
