using CommerceCore.Application.Common.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CommerceCore.Persistence.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("commercecore_integration")
        .WithUsername("postgres")
        .WithPassword("TestPassword123!")
        .Build();

    public IServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var services = new ServiceCollection();

        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<ICurrentUser, TestCurrentUser>();

        services.AddPersistence(_postgres.GetConnectionString());

        Services = services.BuildServiceProvider();

        await using var scope = Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        await dbContext.Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (Services is IAsyncDisposable disposable)
            await disposable.DisposeAsync();

        await _postgres.DisposeAsync();
    }

    private sealed class TestClock : IClock
    {
        public DateTimeOffset UtcNow =>
            new(2026, 8, 15, 15, 0, 0, TimeSpan.Zero);
    }

    private sealed class TestCurrentUser : ICurrentUser
    {
        public string? UserId => "integration-test";
    }
}