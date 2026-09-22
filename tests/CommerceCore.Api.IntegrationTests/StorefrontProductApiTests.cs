using System.Net;
using System.Net.Http.Json;
using CommerceCore.Api.IntegrationTests.Infrastructure;
using Npgsql;

namespace CommerceCore.Api.IntegrationTests;

public sealed class StorefrontProductApiTests(StorefrontApiFixture fixture)
    : IClassFixture<StorefrontApiFixture>
{
    private const string ProductsPath = "/api/storefront/products";

    [Fact]
    public async Task AnonymousRequests_ReturnOnlyVisibleProductsOfHostTenant()
    {
        using HttpClient clientA = fixture.CreateClient(
            fixture.StoreA.HostName);

        using HttpClient clientB = fixture.CreateClient(
            fixture.StoreB.HostName);

        ProductPage pageA = await ReadPageAsync(
            clientA,
            $"{ProductsPath}?tenantId={fixture.StoreB.TenantId}");

        ProductPage pageB = await ReadPageAsync(clientB, ProductsPath);
        ProductPage repeatedA = await ReadPageAsync(clientA, ProductsPath);

        AssertProducts(fixture.StoreA.VisibleProductIds, pageA);
        AssertProducts(fixture.StoreB.VisibleProductIds, pageB);
        AssertProducts(fixture.StoreA.VisibleProductIds, repeatedA);

        Assert.All(pageA.Items, item =>
        {
            Assert.Equal("Storefront product", item.Name);
            Assert.Equal(10m, item.BasePriceAmount);
            Assert.Equal("AZN", item.Currency);
        });
    }

    [Fact]
    public async Task Pagination_StaysWithinTenant_AndRejectsForeignTypeByReturningEmpty()
    {
        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        string path = $"{ProductsPath}?pageSize=1" +
            $"&productTypeId={fixture.StoreA.ProductTypeId}";

        ProductPage first = await ReadPageAsync(client, path);

        ProductItem firstItem = Assert.Single(first.Items);
        Assert.Equal(firstItem.ProductId, first.NextAfterProductId);

        ProductPage second = await ReadPageAsync(
            client,
            $"{path}&afterProductId={first.NextAfterProductId}");

        ProductItem secondItem = Assert.Single(second.Items);

        Assert.Null(second.NextAfterProductId);
        Assert.NotEqual(firstItem.ProductId, secondItem.ProductId);

        Assert.Equal(
            fixture.StoreA.VisibleProductIds.Order(),
            new[] { firstItem.ProductId, secondItem.ProductId }.Order());

        ProductPage foreignType = await ReadPageAsync(
            client,
            $"{ProductsPath}?productTypeId={fixture.StoreB.ProductTypeId}");

        Assert.Empty(foreignType.Items);
        Assert.Null(foreignType.NextAfterProductId);
    }

    [Fact]
    public async Task ConcurrentRequests_DoNotMixTenantContexts()
    {
        using HttpClient clientA = fixture.CreateClient(
            fixture.StoreA.HostName);

        using HttpClient clientB = fixture.CreateClient(
            fixture.StoreB.HostName);

        Task<ProductPage>[] requests = Enumerable.Range(0, 12)
            .Select(index => ReadPageAsync(
                index % 2 == 0 ? clientA : clientB,
                ProductsPath))
            .ToArray();

        ProductPage[] pages = await Task.WhenAll(requests);

        for (int index = 0; index < pages.Length; index++)
        {
            IReadOnlyList<Guid> expected = index % 2 == 0
                ? fixture.StoreA.VisibleProductIds
                : fixture.StoreB.VisibleProductIds;

            AssertProducts(expected, pages[index]);
        }
    }

    [Fact]
    public async Task RuntimeRole_CannotBypassRls_AndSeesNothingWithoutTenant()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // First establish that the seeded products are accessible through HTTP.
        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        ProductPage page = await ReadPageAsync(client, ProductsPath);
        AssertProducts(fixture.StoreA.VisibleProductIds, page);

        await using var connection = new NpgsqlConnection(
            fixture.RuntimeConnectionString);

        await connection.OpenAsync(cancellationToken);

        const string sql = """
            SELECT
                role.rolname,
                role.rolsuper,
                role.rolbypassrls,
                table_info.relrowsecurity,
                table_info.relowner = role.oid AS owns_table,
                (SELECT count(*) FROM catalog.products) AS visible_products
            FROM pg_roles AS role
            CROSS JOIN pg_class AS table_info
            WHERE role.rolname = current_user
              AND table_info.oid = 'catalog.products'::regclass;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(
            cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("commercecore_api_test", reader.GetString(0));
        Assert.False(reader.GetBoolean(1));
        Assert.False(reader.GetBoolean(2));
        Assert.True(reader.GetBoolean(3));
        Assert.False(reader.GetBoolean(4));
        Assert.Equal(0L, reader.GetInt64(5));
    }

    private static async Task<ProductPage> ReadPageAsync(
        HttpClient client,
        string path)
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpResponseMessage response = await client.GetAsync(
            path,
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        ProductPage? page = await response.Content
            .ReadFromJsonAsync<ProductPage>(
                cancellationToken: cancellationToken);

        Assert.NotNull(page);
        return page;
    }

    private static void AssertProducts(
        IReadOnlyList<Guid> expected,
        ProductPage actual)
    {
        Assert.Equal(
            expected.Order(),
            actual.Items.Select(item => item.ProductId).Order());

        Assert.Null(actual.NextAfterProductId);
    }

    public sealed record ProductPage(
        ProductItem[] Items,
        Guid? NextAfterProductId);

    public sealed record ProductItem(
        Guid ProductId,
        Guid ProductTypeId,
        string Name,
        decimal BasePriceAmount,
        string Currency);

    [Fact]
    public async Task Details_ReturnOnlyActiveVariants_WithinHostTenant()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        Guid productId = fixture.StoreA.VisibleProductIds[0];

        using HttpResponseMessage response = await client.GetAsync(
            $"{ProductsPath}/{productId}?tenantId={fixture.StoreB.TenantId}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);

        ProductDetails? details = await response.Content
            .ReadFromJsonAsync<ProductDetails>(
                cancellationToken: cancellationToken);

        Assert.NotNull(details);
        Assert.Equal(productId, details.ProductId);
        Assert.Equal(fixture.StoreA.ProductTypeId, details.ProductTypeId);
        Assert.Equal("Storefront product", details.Name);
        Assert.Equal(10m, details.BasePriceAmount);
        Assert.Equal("AZN", details.Currency);

        VariantDetails variant = Assert.Single(details.Variants);

        Assert.Equal(
            fixture.StoreA.FirstProductDefaultVariantId,
            variant.ProductVariantId);

        Assert.True(variant.IsDefault);
        Assert.False(string.IsNullOrWhiteSpace(variant.Sku));
        Assert.Equal(10m, variant.BasePriceAmount);
        Assert.Equal("AZN", variant.Currency);
        Assert.NotNull(variant.Options);
        Assert.Single(variant.Options);
        Assert.Equal("medium", variant.Options["size"]);
    }

    [Fact]
    public async Task Details_ForeignHiddenAndMissingProducts_ReturnNotFound()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        IEnumerable<Guid> invisibleIds = fixture.StoreA.HiddenProductIds
            .Concat(fixture.StoreB.VisibleProductIds)
            .Append(Guid.NewGuid());

        foreach (Guid productId in invisibleIds)
        {
            using HttpResponseMessage response = await client.GetAsync(
                $"{ProductsPath}/{productId}",
                cancellationToken);

            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.True(response.Headers.CacheControl?.NoStore == true);

            string body = await response.Content.ReadAsStringAsync(
                cancellationToken);

            Assert.Empty(body);
        }
    }

    [Fact]
    public async Task Details_EmptyProductId_ReturnsBadRequest()
    {
        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        using HttpResponseMessage response = await client.GetAsync(
            $"{ProductsPath}/{Guid.Empty}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Details_UnknownStorefront_ReturnsBadRequest()
    {
        using HttpClient client = fixture.CreateClient(
            "unknown.example.com");

        using HttpResponseMessage response = await client.GetAsync(
            $"{ProductsPath}/{fixture.StoreA.VisibleProductIds[0]}",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Details_VariantWithoutOptions_ReturnsEmptyObject()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        using HttpClient client = fixture.CreateClient(
            fixture.StoreA.HostName);

        Guid productId = fixture.StoreA.VisibleProductIds[1];

        using HttpResponseMessage response = await client.GetAsync(
            $"{ProductsPath}/{productId}",
            cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        ProductDetails? details = await response.Content
            .ReadFromJsonAsync<ProductDetails>(
                cancellationToken: cancellationToken);

        Assert.NotNull(details);

        VariantDetails variant = Assert.Single(details.Variants);

        Assert.NotNull(variant.Options);
        Assert.Empty(variant.Options);
    }

    public sealed record ProductDetails(
        Guid ProductId,
        Guid ProductTypeId,
        string Name,
        decimal BasePriceAmount,
        string Currency,
        VariantDetails[] Variants);

    public sealed record VariantDetails(
        Guid ProductVariantId,
        string Sku,
        decimal BasePriceAmount,
        string Currency,
        bool IsDefault,
        Dictionary<string, string> Options);
}
