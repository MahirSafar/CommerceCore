using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace CommerceCore.Api.Configuration;

internal static class ObservabilityExtensions
{
    public static void AddObservability(this IHostApplicationBuilder builder)
    {
        if (!string.IsNullOrWhiteSpace(
                builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]))
        {
            const string serviceName = "CommerceCore.Api";

            string serviceVersion = typeof(Program)
                .Assembly
                .GetName()
                .Version?
                .ToString() ?? "unknown";

            ResourceBuilder resourceBuilder = ResourceBuilder
                .CreateDefault()
                .AddService(
                    serviceName: serviceName,
                    serviceVersion: serviceVersion)
                .AddAttributes(
                    new Dictionary<string, object>
                    {
                        ["deployment.environment.name"] =
                            builder.Environment.EnvironmentName
                    });

            builder.Logging.AddOpenTelemetry(logging =>
            {
                logging.SetResourceBuilder(resourceBuilder);
                logging.IncludeFormattedMessage = true;
                logging.IncludeScopes = true;
                logging.ParseStateValues = true;
                logging.AddOtlpExporter();
            });

            builder.Services.AddOpenTelemetry()
                .ConfigureResource(resource => resource
                    .AddService(
                        serviceName: serviceName,
                        serviceVersion: serviceVersion)
                    .AddAttributes(
                        new Dictionary<string, object>
                        {
                            ["deployment.environment.name"] =
                                builder.Environment.EnvironmentName
                        }))
                .WithTracing(tracing => tracing
                    .AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddOtlpExporter())
                .WithMetrics(metrics => metrics
                    .AddAspNetCoreInstrumentation()
                    .AddMeter(
                        "Microsoft.AspNetCore.Hosting",
                        "Microsoft.AspNetCore.Server.Kestrel",
                        "System.Runtime")
                    .AddOtlpExporter());
        }
    }
}
