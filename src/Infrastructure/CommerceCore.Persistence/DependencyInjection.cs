using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.Interceptors;
using CommerceCore.Persistence.ProductTypes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence;

public static class DependencyInjection
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddScoped<AuditingSaveChangesInterceptor>();
        services.AddScoped<OutboxSaveChangesInterceptor>();

        services.AddScoped<IProductTypeSchemaCoordinator, ProductTypeSchemaCoordinator>();
        services.AddScoped<IAttributeDefinitionRegistry, AttributeDefinitionRegistry>();
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

                AuditingSaveChangesInterceptor auditingInterceptor =
                    serviceProvider.GetRequiredService<AuditingSaveChangesInterceptor>();

                OutboxSaveChangesInterceptor outboxInterceptor =
                    serviceProvider.GetRequiredService<OutboxSaveChangesInterceptor>();

                options.AddInterceptors(
                    auditingInterceptor,
                    outboxInterceptor);
            });

        services.AddScoped<ICommerceCoreDbContext>(serviceProvider => serviceProvider.GetRequiredService<CommerceCoreDbContext>());
        return services;
    }
}
