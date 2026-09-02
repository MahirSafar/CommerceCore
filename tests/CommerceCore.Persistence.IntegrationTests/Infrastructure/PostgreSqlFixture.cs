using CommerceCore.Application;
using CommerceCore.Application.Catalog;
using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommerceCore.Persistence.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18.6")
        .WithDatabase("commercecore_integration")
        .WithUsername("postgres")
        .WithPassword("TestPassword123!")
        .Build();

    private DbContextOptions<CommerceCoreDbContext> _adminDbContextOptions = null!;

    public TenantId PrimaryTenantId { get; } = TenantId.New();

    public TenantId SecondaryTenantId { get; } = TenantId.New();

    public IServiceProvider Services { get; private set; } = null!;

    public TenantId SetTenantForCurrentTest()
    {
        TenantId tenantId = PrimaryTenantId;

        Services.GetRequiredService<TestTenantContext>()
            .SetTenant(tenantId);

        return tenantId;
    }

    public async Task<TenantId> CreateTenantAsync(
        CancellationToken cancellationToken)
    {
        TenantId tenantId = TenantId.New();

        await using var dbContext =
            new CommerceCoreDbContext(_adminDbContextOptions);

        dbContext.Tenants.Add(new Tenant
        {
            Id = tenantId,
            Slug = $"integration-{tenantId.Value:N}",
            Name = "Integration Test Tenant",
            Status = "Active"
        });

        await dbContext.SaveChangesAsync(cancellationToken);

        return tenantId;
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        string adminConnectionString = _postgres.GetConnectionString();

        var migrationOptions = new DbContextOptionsBuilder<CommerceCoreDbContext>()
            .UseNpgsql(adminConnectionString)
            .Options;

        _adminDbContextOptions = migrationOptions;

        await using (var migrationDb = new CommerceCoreDbContext(migrationOptions))
        {
            await migrationDb.Database.MigrateAsync();

            migrationDb.Tenants.AddRange(
                new Tenant
                {
                    Id = PrimaryTenantId,
                    Slug = "integration-primary",
                    Name = "Integration Primary Tenant",
                    Status = "Active"
                },
                new Tenant
                {
                    Id = SecondaryTenantId,
                    Slug = "integration-secondary",
                    Name = "Integration Secondary Tenant",
                    Status = "Active"
                });

            await migrationDb.SaveChangesAsync();
        }

        await using (var adminConnection = new NpgsqlConnection(adminConnectionString))
        {
            await adminConnection.OpenAsync();

            await using var command = adminConnection.CreateCommand();

            command.CommandText = """
                CREATE ROLE commercecore_app
                    LOGIN
                    PASSWORD 'TestAppPassword123!'
                    NOSUPERUSER
                    NOCREATEDB
                    NOCREATEROLE
                    NOINHERIT
                    NOBYPASSRLS;

                GRANT USAGE ON SCHEMA catalog, outbox, platform TO commercecore_app;
                GRANT SELECT, INSERT, UPDATE, DELETE
                    ON ALL TABLES IN SCHEMA catalog, outbox
                    TO commercecore_app;
                GRANT SELECT
                    ON ALL TABLES IN SCHEMA platform
                    TO commercecore_app;
                GRANT USAGE, SELECT
                    ON ALL SEQUENCES IN SCHEMA catalog, outbox, platform
                    TO commercecore_app;
                """;

            await command.ExecuteNonQueryAsync();
        }

        var applicationConnectionString =
            new NpgsqlConnectionStringBuilder(adminConnectionString)
            {
                Username = "commercecore_app",
                Password = "TestAppPassword123!"
            }.ConnectionString;

        var services = new ServiceCollection();

        services.AddSingleton<IClock, TestClock>();
        services.AddSingleton<ICurrentUser, TestCurrentUser>();
        services.AddSingleton<TestTenantContext>();
        services.AddSingleton<ITenantContext>(sp => sp.GetRequiredService<TestTenantContext>());

        services.AddPersistence(applicationConnectionString);

        services.AddCatalogApplication();

        Services = services.BuildServiceProvider();
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