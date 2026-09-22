using CommerceCore.Outbox.Worker.Configuration;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.AddOutboxWorker();

IHost host = builder.Build();

await host.RunAsync();