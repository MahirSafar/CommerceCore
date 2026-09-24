using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
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
    public const string AzerbaijaniHostName = "store-a-az.example.com";
    public const string FallbackHostName = "store-a-de.example.com";

    private readonly PostgreSqlContainer _postgres =
        new PostgreSqlBuilder("postgres:18.6")
            .WithDatabase("commercecore_api_tests")
            .WithUsername("postgres")
            .WithPassword(Guid.NewGuid().ToString("N"))
            .Build();

    private ApiApplication? _application;

    public StoreData StoreA { get; private set; } = null!;
    public StoreData StoreB { get; private set; } = null!;
    public StoreData PaginationStore { get; private set; } = null!;

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

            database.Storefronts.AddRange(
                Storefront.Create(
                    StorefrontId.New(),
                    TenantId.From(StoreA.TenantId),
                    AzerbaijaniHostName,
                    MarketId.From("AZ"),
                    "az"),
                Storefront.Create(
                    StorefrontId.New(),
                    TenantId.From(StoreA.TenantId),
                    FallbackHostName,
                    MarketId.From("AZ"),
                    "de"));

            await database.SaveChangesAsync(cancellationToken);

            StoreB = await SeedStoreAsync(
                database,
                "store-b.example.com",
                cancellationToken);

            PaginationStore = await SeedStoreAsync(
                database,
                "pagination.example.com",
                cancellationToken,
                additionalActiveVariants: 24);
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
        CancellationToken cancellationToken,
        int additionalActiveVariants = 0)
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

        AttributeDefinition size = productType.DefineAttribute(
            AttributeKey.Create("size"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired: false,
            displayOrder: 0);

        productType.AddAttributeOption(
            size.Id,
            AttributeOptionCode.Create("small"),
            displayOrder: 0);

        productType.AddAttributeOption(
            size.Id,
            AttributeOptionCode.Create("medium"),
            displayOrder: 1);

        productType.AddAttributeOption(
            size.Id,
            AttributeOptionCode.Create("large"),
            displayOrder: 2);

        for (int index = 0; index < additionalActiveVariants; index++)
        {
            productType.AddAttributeOption(
                size.Id,
                AttributeOptionCode.Create($"extra-{index:D2}"),
                displayOrder: index + 3);
        }

        database.ProductTypes.Add(productType);
        await database.SaveChangesAsync(cancellationToken);

        Product first = CreateProduct(tenantId, productType.Id);

        for (int index = 0; index < additionalActiveVariants; index++)
        {
            ProductVariant variant = first.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                Money.Create(10m, "AZN"),
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("size"),
                    AttributeValue.SingleSelect.Create($"extra-{index:D2}")),
                isDefault: false);

            first.ActivateVariant(variant.Id);
        }

        Product second = CreateProduct(
            tenantId,
            productType.Id,
            includeDefaultOptions: false);

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
            [first.Id.Value, second.Id.Value],
            [draft.Id.Value, inactive.Id.Value, archived.Id.Value],
            first.Variants.Single(variant => variant.IsDefault).Id.Value,
            first.Variants
                .Where(variant => variant.Status == ProductVariantStatus.Active)
                .Select(variant => variant.Id.Value)
                .ToArray());
    }

    private static Product CreateProduct(
        TenantId tenantId,
        ProductTypeId productTypeId,
        bool activate = true,
        bool includeDefaultOptions = true)
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Storefront product"),
                new KeyValuePair<LanguageCode, string>(
                    LanguageCode.Create("az"),
                    "Vitrin məhsulu")
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
            AttributeValueBag defaultOptions = includeDefaultOptions
                ? AttributeValueBag.Empty.With(
                    AttributeKey.Create("size"),
                    AttributeValue.SingleSelect.Create("medium"))
                : AttributeValueBag.Empty;

            ProductVariant defaultVariant = product.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                price,
                defaultOptions,
                isDefault: true);

            product.ActivateVariant(defaultVariant.Id);

            product.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                price,
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("size"),
                    AttributeValue.SingleSelect.Create("small")),
                isDefault: false);

            ProductVariant inactiveVariant = product.AddVariant(
                VariantSku.Create($"sku_{Guid.NewGuid():N}"[..20]),
                price,
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("size"),
                    AttributeValue.SingleSelect.Create("large")),
                isDefault: false);

            product.ActivateVariant(inactiveVariant.Id);
            product.DeactivateVariant(inactiveVariant.Id);

            product.Activate();
        }

        return product;
    }

    public sealed record StoreData(
        Guid TenantId,
        string HostName,
        Guid ProductTypeId,
        IReadOnlyList<Guid> VisibleProductIds,
        IReadOnlyList<Guid> HiddenProductIds,
        Guid FirstProductDefaultVariantId,
        IReadOnlyList<Guid> FirstProductActiveVariantIds);

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
