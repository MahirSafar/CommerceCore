using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Infrastructure.Common.Time;
using CommerceCore.Persistence;
using CommerceCore.Persistence.Interceptors;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CommerceCore.Api.IntegrationTests.Infrastructure;

public sealed class StorefrontApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18.6")
            .WithDatabase("commercecore_api_tests")
            .WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

    private ApiApplication? _application;

    public StoreData StoreA { get; private set; } = null!;
    public StoreData StoreB { get; private set; } = null!;

    internal string RuntimeConnectionString { get; private set; } =
        string.Empty;

    public async ValueTask InitializeAsync()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        await _postgres.StartAsync(cancellationToken);

        string adminConnectionString = _postgres.GetConnectionString();

        var options = new DbContextOptionsBuilder<CommerceCoreDbContext>()
            .UseNpgsql(adminConnectionString)
            .AddInterceptors(new AuditingSaveChangesInterceptor(
                new SystemClock(),
                new SeedUser()))
            .Options;

        await using (var database = new CommerceCoreDbContext(options))
        {
            await database.Database.MigrateAsync(cancellationToken);

            StoreA = await SeedStoreAsync(
                database,
                "store-a.example.com",
                cancellationToken);

            StoreB = await SeedStoreAsync(
                database,
                "store-b.example.com",
                cancellationToken);
        }

        string runtimePassword = Guid.NewGuid().ToString("N");

        await using (var connection = new NpgsqlConnection(
            adminConnectionString))
        {
            await connection.OpenAsync(cancellationToken);

            // Only a generated hexadecimal password is interpolated.
            string sql = $"""
                CREATE ROLE commercecore_api_test
                    LOGIN PASSWORD '{runtimePassword}'
                    NOSUPERUSER NOCREATEDB NOCREATEROLE
                    NOINHERIT NOBYPASSRLS;

                GRANT CONNECT ON DATABASE commercecore_api_tests
                    TO commercecore_api_test;

                GRANT USAGE ON SCHEMA catalog, outbox, platform
                    TO commercecore_api_test;

                GRANT SELECT, INSERT, UPDATE, DELETE
                    ON ALL TABLES IN SCHEMA catalog, outbox
                    TO commercecore_api_test;

                GRANT SELECT ON ALL TABLES IN SCHEMA platform
                    TO commercecore_api_test;

                GRANT USAGE, SELECT
                    ON ALL SEQUENCES IN SCHEMA catalog, outbox
                    TO commercecore_api_test;
                """;

            await using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        RuntimeConnectionString = new NpgsqlConnectionStringBuilder(
            adminConnectionString)
        {
            Username = "commercecore_api_test",
            Password = runtimePassword,
            MaxPoolSize = 1
        }.ConnectionString;

        _application = new ApiApplication(RuntimeConnectionString);
    }

    public HttpClient CreateClient(string hostName)
    {
        ArgumentNullException.ThrowIfNull(_application);

        return _application.CreateClient(
            new WebApplicationFactoryClientOptions
            {
                BaseAddress = new Uri($"https://{hostName}"),
                AllowAutoRedirect = false
            });
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (_application is not null)
            {
                await _application.DisposeAsync();
            }
        }
        finally
        {
            await _postgres.DisposeAsync();
        }
    }

    private static async Task<StoreData> SeedStoreAsync(
        CommerceCoreDbContext database,
        string hostName,
        CancellationToken cancellationToken)
    {
        TenantId tenantId = TenantId.New();

        database.Tenants.Add(Tenant.Create(
            tenantId,
            $"tenant-{tenantId.Value:N}",
            hostName));

        database.Storefronts.Add(Storefront.Create(
            StorefrontId.New(),
            tenantId,
            hostName,
            MarketId.From("AZ"),
            "en"));

        await database.SaveChangesAsync(cancellationToken);

        ProductType productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..20]),
            isAssignable: true);

        database.ProductTypes.Add(productType);
        await database.SaveChangesAsync(cancellationToken);

        Product first = CreateProduct(tenantId, productType.Id);
        Product second = CreateProduct(tenantId, productType.Id);

        Product draft = CreateProduct(
            tenantId,
            productType.Id,
            activate: false);

        Product inactive = CreateProduct(tenantId, productType.Id);
        inactive.Deactivate();

        Product archived = CreateProduct(tenantId, productType.Id);
        archived.Archive(DateTimeOffset.UtcNow, "api-integration-test");

        database.Products.AddRange(
            first,
            second,
            draft,
            inactive,
            archived);

        await database.SaveChangesAsync(cancellationToken);

        return new StoreData(
            tenantId.Value,
            hostName,
            productType.Id.Value,
            [first.Id.Value, second.Id.Value]);
    }

    private static Product CreateProduct(
        TenantId tenantId,
        ProductTypeId productTypeId,
        bool activate = true)
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Storefront product")
            ]);

        Money price = Money.Create(10m, "AZN");

        Product product = Product.Create(
            tenantId,
            name,
            price,
            productTypeId,
            DateTimeOffset.UtcNow);

        if (activate)
        {
            ProductVariant variant = product.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                price,
                AttributeValueBag.Empty,
                isDefault: true);

            product.ActivateVariant(variant.Id);
            product.Activate();
        }

        return product;
    }

    public sealed record StoreData(
        Guid TenantId,
        string HostName,
        Guid ProductTypeId,
        IReadOnlyList<Guid> VisibleProductIds);

    private sealed class SeedUser : ICurrentUser
    {
        public string? UserId => "api-integration-test";
    }

    private sealed class ApiApplication(string connectionString)
        : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting(
                "ConnectionStrings:CommerceCoreDatabase",
                connectionString);
        }
    }
}
