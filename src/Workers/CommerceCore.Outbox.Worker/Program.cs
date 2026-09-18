using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.RabbitMq;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services
    .AddOptions<RabbitMqOptions>()
    .BindConfiguration(RabbitMqOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddHostedService<RabbitMqTopologyInitializer>();

IHost host = builder.Build();

await host.RunAsync();