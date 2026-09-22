using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.Dispatching;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class OutboxWorkerLifecycleTests
{
    [Fact]
    public async Task DisabledWorker_DoesNotCreateDatabaseScope()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        var scopeFactory = Substitute.For<IServiceScopeFactory>();

        using var worker = new OutboxWorker(
            scopeFactory,
            Options.Create(new RabbitMqOptions { DispatcherEnabled = false }),
            NullLogger<OutboxWorker>.Instance);

        await worker.StartAsync(token);

        Assert.NotNull(worker.ExecuteTask);
        await worker.ExecuteTask.WaitAsync(
            TimeSpan.FromSeconds(5),
            token);

        await worker.StopAsync(token);

        scopeFactory.DidNotReceive().CreateScope();
    }

    [Fact]
    public void TenantContext_IsScopedAndCannotBeReassigned()
    {
        var services = new ServiceCollection();
        services.AddScoped<WorkerTenantContext>();
        services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<WorkerTenantContext>());

        using ServiceProvider provider = services.BuildServiceProvider();

        using IServiceScope firstScope = provider.CreateScope();
        using IServiceScope secondScope = provider.CreateScope();

        var first = firstScope.ServiceProvider
            .GetRequiredService<WorkerTenantContext>();
        var second = secondScope.ServiceProvider
            .GetRequiredService<WorkerTenantContext>();

        TenantId tenantId = TenantId.New();

        first.Initialize(tenantId);

        Assert.Equal(tenantId, first.TenantId);
        Assert.Null(second.TenantId);
        Assert.Same(
            first,
            firstScope.ServiceProvider.GetRequiredService<ITenantContext>());

        Assert.Throws<InvalidOperationException>(
            () => first.Initialize(TenantId.New()));
    }
}
