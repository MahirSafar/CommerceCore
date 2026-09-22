using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using NSubstitute;
using CommerceCore.Api.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using CommerceCore.Platform.Identity;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;

namespace CommerceCore.Api.UnitTests;

public class AuthorizationRegressionTests : IClassFixture<WebApplicationFactory<Program>>
{
    private const string TestIssuer = "https://test-issuer";
    private const string TestAudience = "test-audience";
    private static readonly SymmetricSecurityKey TestKey = new(Encoding.UTF8.GetBytes("SuperSecretKeyThatIsAtLeast32BytesLongForHS256!!!"));
    private static readonly SymmetricSecurityKey InvalidTestKey = new(Encoding.UTF8.GetBytes("AnotherSuperSecretKeyThatIsAtLeast32Bytes!!!"));

    private readonly WebApplicationFactory<Program> _factory;

    public AuthorizationRegressionTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:CommerceCoreDatabase", "Host=localhost;Database=dummy;Username=postgres;Password=postgres");

            builder.ConfigureTestServices(services =>
            {
                services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidateAudience = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = TestIssuer,
                        ValidAudience = TestAudience,
                        IssuerSigningKey = TestKey,
                        ClockSkew = TimeSpan.Zero
                    };
                    options.Authority = string.Empty;
                    options.MetadataAddress = string.Empty;
                });

                // Override health checks to always return healthy so /health/ready returns 200
                services.Configure<HealthCheckServiceOptions>(options =>
                {
                    options.Registrations.Clear();
                    options.Registrations.Add(new HealthCheckRegistration(
                        "dummy",
                        new DummyHealthCheck(),
                        HealthStatus.Unhealthy,
                        new[] { "ready" }));
                });

                var testTenantId =
                    CommerceCore.Platform.Contracts.TenantId.New();

                var mockTenantStore = NSubstitute.Substitute.For<CommerceCore.Platform.ControlPlane.IPlatformTenantStore>();
                mockTenantStore.GetActiveMembershipAsync(
                        testTenantId,
                        "test-user",
                        NSubstitute.Arg.Any<CancellationToken>())
                    .Returns(CommerceCore.Platform.ControlPlane.Entities.TenantMembership.Create(
                        testTenantId,
                        "test-user",
                        CommerceCore.Platform.ControlPlane.Entities.TenantMembershipRoles.Admin));
                mockTenantStore.GetStorefrontByHostAsync(NSubstitute.Arg.Any<string>(), NSubstitute.Arg.Any<CancellationToken>())
                    .Returns(CommerceCore.Platform.ControlPlane.Entities.Storefront.Create(
                        CommerceCore.Platform.Contracts.StorefrontId.New(),
                        testTenantId,
                        "localhost",
                        CommerceCore.Platform.Contracts.MarketId.From("AZ"),
                        "az-AZ"));
                services.AddScoped(_ => mockTenantStore);
            });

            builder.UseEnvironment("Development");
        });
    }

    private HttpClient CreateClientWithToken(
        string[] scopes,
        string? audience = null,
        SymmetricSecurityKey? signingKey = null,
        DateTime? expires = null)
    {
        var client = _factory.CreateClient();

        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, "test-user"),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (scopes.Length > 0)
        {
            claims.Add(new Claim("scope", string.Join(" ", scopes)));
        }

        var key = signingKey ?? TestKey;
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            TestIssuer,
            audience ?? TestAudience,
            claims,
            expires: expires ?? DateTime.UtcNow.AddHours(1),
            signingCredentials: credentials);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwt = tokenHandler.WriteToken(token);

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        return client;
    }

    [Fact]
    public async Task GetProduct_WithoutToken_ReturnsUnauthorized()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithOnlyReadScope_ReturnsForbidden()
    {
        var client = CreateClientWithToken(["catalog.read"]);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithManageScopeButNoRead_ReturnsForbidden()
    {
        var client = CreateClientWithToken(["catalog.manage"]);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithManageScopeAndEmptyBody_ReturnsBadRequest()
    {
        var client = CreateClientWithToken(["catalog.read", "catalog.manage"]);

        var content = new StringContent("", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostProductType_WithSchemaManageScopeButNoRead_ReturnsForbidden()
    {
        var client = CreateClientWithToken(["catalog.schema.manage"]);
        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/product-types", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProductType_WithSchemaManageScopeAndEmptyBody_ReturnsBadRequest()
    {
        var client = CreateClientWithToken(["catalog.read", "catalog.schema.manage"]);

        var content = new StringContent("", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/product-types", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PostProductType_WithCatalogManagerScopes_ReturnsForbidden()
    {
        var client = CreateClientWithToken(["catalog.read", "catalog.manage"]);

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/product-types", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task PostProduct_WithSchemaManagerScopes_ReturnsForbidden()
    {
        var client = CreateClientWithToken(["catalog.read", "catalog.schema.manage"]);

        var content = new StringContent("{}", Encoding.UTF8, "application/json");
        var response = await client.PostAsync("/api/products", content, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AnyEndpoint_WithInvalidAudienceToken_ReturnsUnauthorized()
    {
        var client = CreateClientWithToken(["catalog.read"], audience: "invalid-audience");
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnyEndpoint_WithInvalidSigningKeyToken_ReturnsUnauthorized()
    {
        var client = CreateClientWithToken(["catalog.read"], signingKey: InvalidTestKey);
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AnyEndpoint_WithExpiredToken_ReturnsUnauthorized()
    {
        var client = CreateClientWithToken(["catalog.read"], expires: DateTime.UtcNow.AddMinutes(-5));
        var response = await client.GetAsync($"/api/products/{Guid.NewGuid()}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task HealthEndpoints_WithoutToken_ReturnsOk(string endpoint)
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync(endpoint, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public void CatalogEndpoints_RequireExpectedAuthorizationPolicies()
    {
        RouteEndpoint[] endpoints = _factory.Services
            .GetServices<EndpointDataSource>()
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Where(endpoint =>
                endpoint.RoutePattern.RawText?.StartsWith("/api/", StringComparison.Ordinal) == true)
            .ToArray();

        Assert.NotEmpty(endpoints);

        foreach (RouteEndpoint endpoint in endpoints)
        {
            if (endpoint.RoutePattern.RawText == "/api/storefront/products")
            {
                Assert.NotNull(
                    endpoint.Metadata.GetMetadata<PublicStorefrontReadMetadata>());
                Assert.NotNull(
                    endpoint.Metadata.GetMetadata<IAllowAnonymous>());
                Assert.Empty(
                    endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>());

                var publicMethods = endpoint.Metadata.GetMetadata<HttpMethodMetadata>();
                Assert.NotNull(publicMethods);
                Assert.Equal("GET", Assert.Single(publicMethods.HttpMethods));
                continue;
            }

            Assert.Null(
                endpoint.Metadata.GetMetadata<PublicStorefrontReadMetadata>());
            Assert.Null(
                endpoint.Metadata.GetMetadata<IAllowAnonymous>());

            string[] policies = endpoint.Metadata
                .GetOrderedMetadata<IAuthorizeData>()
                .Select(data => data.Policy)
                .OfType<string>()
                .ToArray();

            Assert.Contains(AuthorizationPolicies.CatalogRead, policies);

            HttpMethodMetadata? methods =
                endpoint.Metadata.GetMetadata<HttpMethodMetadata>();

            bool isMutation = methods?.HttpMethods.Any(
                method => method is not "GET" and not "HEAD" and not "OPTIONS") == true;

            if (!isMutation)
            {
                continue;
            }

            string expectedWritePolicy =
                endpoint.RoutePattern.RawText!.StartsWith(
                    "/api/product-types",
                    StringComparison.Ordinal)
                    ? AuthorizationPolicies.CatalogSchemaManage
                    : AuthorizationPolicies.CatalogManage;

            Assert.Contains(expectedWritePolicy, policies);
        }
    }

    private class DummyHealthCheck : IHealthCheck
    {
        public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(HealthCheckResult.Healthy("A OK"));
        }
    }
}
