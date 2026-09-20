using CommerceCore.Outbox.Worker.Dispatching;
using CommerceCore.Outbox.Worker.RabbitMq;
using CommerceCore.Persistence;
using CommerceCore.Persistence.Interceptors;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Outbox.Worker.Configuration;

public static class WorkerServiceRegistration
{
    public static void AddOutboxWorker(this IHostApplicationBuilder builder)
    {
        builder.Services
            .AddOptions<RabbitMqOptions>()
            .BindConfiguration(RabbitMqOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        bool enabled = builder.Configuration.GetValue<bool>(
            $"{RabbitMqOptions.SectionName}:DispatcherEnabled");

        if (enabled)
        {
            string connectionString =
                builder.Configuration.GetConnectionString("CommerceCoreDatabase")
                ?? throw new InvalidOperationException(
                    "The worker database connection string is required.");

            builder.Services.AddScoped<WorkerTenantContext>();
            builder.Services.AddScoped<ITenantContext>(services =>
                services.GetRequiredService<WorkerTenantContext>());

            builder.Services.AddScoped<TenantSessionInterceptor>();
            builder.Services.AddScoped<IOutboxDeliveryStore, OutboxDeliveryStore>();

            builder.Services.AddDbContext<PlatformReadDbContext>(options =>
                options.UseNpgsql(connectionString));

            builder.Services.AddDbContext<CommerceCoreDbContext>(
                (services, options) =>
                {
                    options.UseNpgsql(connectionString);
                    options.AddInterceptors(
                        services.GetRequiredService<TenantSessionInterceptor>());
                });
        }

        builder.Services.AddHostedService<RabbitMqTopologyInitializer>();
        builder.Services.AddHostedService<OutboxWorker>();
    }
}
