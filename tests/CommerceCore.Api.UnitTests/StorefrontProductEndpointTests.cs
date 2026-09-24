using System.Net;
using System.Text.Json;
using CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace CommerceCore.Api.UnitTests;

public sealed class StorefrontProductEndpointTests(
    WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly TenantId _tenantId = TenantId.New();
    private readonly IPlatformTenantStore _tenantStore = Substitute.For<IPlatformTenantStore>();
    private readonly IStorefrontProductReader _reader = Substitute.For<IStorefrontProductReader>();

    [Fact]
    public async Task AnonymousRequest_UsesHostTenant_AndReturnsProducts()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        Guid productId = Guid.NewGuid();
        Guid productTypeId = Guid.NewGuid();
        Guid afterProductId = Guid.NewGuid();

        _reader.ReadAsync(
                Arg.Any<ListStorefrontProductsQuery>(),
                Arg.Any<CancellationToken>())
            .Returns(new StorefrontProductPage(
                [
                    new StorefrontProductListItem(
                        productId,
                        productTypeId,
                        "Test product",
                        10m,
                        "AZN")
                ],
                null));

        await using var testFactory = CreateFactory();
        using var client = CreateClient(testFactory);

        string url = $"/api/storefront/products?pageSize=2" +
            $"&afterProductId={afterProductId}" +
            $"&productTypeId={productTypeId}" +
            $"&tenantId={Guid.NewGuid()}";

        using var response = await client.GetAsync(url, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        using JsonDocument body = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync(cancellationToken));

        JsonElement items = body.RootElement.GetProperty("items");
        Assert.Equal(1, items.GetArrayLength());
        Assert.Equal(
            productId,
            items[0].GetProperty("productId").GetGuid());

        await _reader.Received(1).ReadAsync(
            Arg.Is<ListStorefrontProductsQuery>(query =>
                query.PageSize == 2 &&
                query.AfterProductId == afterProductId &&
                query.ProductTypeId == productTypeId),
            Arg.Any<CancellationToken>());

        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("?pageSize=0")]
    [InlineData("?pageSize=101")]
    [InlineData("?afterProductId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?productTypeId=00000000-0000-0000-0000-000000000000")]
    [InlineData("?afterProductId=invalid")]
    public async Task InvalidQuery_ReturnsBadRequest_WithoutReadingProducts(
        string queryString)
    {
        await using var testFactory = CreateFactory();
        using var client = CreateClient(testFactory);

        using var response = await client.GetAsync(
            "/api/storefront/products" + queryString,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _reader.DidNotReceive().ReadAsync(
            Arg.Any<ListStorefrontProductsQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownHost_ReturnsBadRequest_WithoutReadingProducts()
    {
        await using var testFactory = CreateFactory();
        using var client = CreateClient(testFactory);

        using var response = await client.GetAsync(
            "https://unknown.example.com/api/storefront/products",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _reader.DidNotReceive().ReadAsync(
            Arg.Any<ListStorefrontProductsQuery>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AdminEndpoint_StillRequiresAuthentication()
    {
        await using var testFactory = CreateFactory();
        using var client = CreateClient(testFactory);

        using var response = await client.GetAsync(
            $"/api/products/{Guid.NewGuid()}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory()
    {
        _tenantStore.GetStorefrontByHostAsync(
                "store.example.com",
                Arg.Any<CancellationToken>())
            .Returns(Storefront.Create(
                StorefrontId.New(),
                _tenantId,
                "store.example.com",
                MarketId.From("AZ"),
                "az-AZ"));

        return factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("AllowedHosts", "*");
            builder.UseSetting(
                "ConnectionStrings:CommerceCoreDatabase",
                "Host=localhost;Database=unused;Username=unused;Password=unused");

            builder.ConfigureTestServices(services =>
            {
                services.AddScoped<IPlatformTenantStore>(_ => _tenantStore);
                services.AddScoped<IStorefrontProductReader>(provider =>
                {
                    var tenantContext = provider.GetRequiredService<ITenantContext>();
                    Assert.True(tenantContext.IsResolved);
                    Assert.Equal(_tenantId, tenantContext.TenantId);
                    return _reader;
                });
            });
        });
    }

    private static HttpClient CreateClient(
        WebApplicationFactory<Program> testFactory)
    {
        return testFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://store.example.com"),
            AllowAutoRedirect = false
        });
    }
}
