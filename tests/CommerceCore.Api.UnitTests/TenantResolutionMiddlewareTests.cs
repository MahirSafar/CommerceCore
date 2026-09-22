using System.Security.Claims;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using CommerceCore.Platform.ControlPlane.Entities;
using CommerceCore.Platform.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CommerceCore.Api.UnitTests;

public sealed class TenantResolutionMiddlewareTests
{
    private readonly IPlatformTenantStore _tenantStore = Substitute.For<IPlatformTenantStore>();

    [Fact]
    public async Task Catalog_Route_Resolves_Tenant_From_Host_And_Active_Membership()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("store1.example.com");

        const string userSub = "auth0|catalog-user-123";

        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userSub)],
                "Bearer"));

        var tenantId = Guid.NewGuid();
        var storefrontId = Guid.NewGuid();
        var storefront = Storefront.Create(
            StorefrontId.From(storefrontId),
            TenantId.From(tenantId),
            "store1.example.com",
            MarketId.From("AZ"),
            "az-AZ");

        _tenantStore.GetStorefrontByHostAsync("store1.example.com", Arg.Any<CancellationToken>())
            .Returns(storefront);

        _tenantStore.GetActiveMembershipAsync(
                TenantId.From(tenantId),
                userSub,
                Arg.Any<CancellationToken>())
            .Returns(TenantMembership.Create(
                TenantId.From(tenantId),
                userSub,
                TenantMembershipRoles.Admin));

        var nextCalled = false;
        var middleware = CreatePipeline(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantStore);

        // Assert
        Assert.True(nextCalled);
        var resolved = context.Items["__TenantContext"] as ITenantContext;
        Assert.NotNull(resolved);
        Assert.True(resolved.IsResolved);
        Assert.Equal(tenantId, resolved.TenantId?.Value);
        Assert.Equal(storefrontId, resolved.StorefrontId?.Value);
    }

    [Fact]
    public async Task Storefront_Route_Returns_400_When_Host_Unknown()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/storefront/products";
        context.Request.Host = new HostString("unknown.example.com");

        _tenantStore.GetStorefrontByHostAsync("unknown.example.com", Arg.Any<CancellationToken>())
            .Returns((Storefront?)null);

        var nextCalled = false;
        var middleware = CreatePipeline(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantStore);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Admin_Route_Resolves_Tenant_From_Host_And_Active_Membership()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/catalog/products";
        context.Request.Host = new HostString("admin.example.com");

        const string userSub = "auth0|admin-user-123";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userSub)],
                "Bearer"));

        TenantId tenantId = TenantId.New();

        var storefront = Storefront.Create(
            StorefrontId.New(),
            tenantId,
            "admin.example.com",
            MarketId.From("AZ"),
            "az-AZ");

        _tenantStore.GetStorefrontByHostAsync(
                "admin.example.com",
                Arg.Any<CancellationToken>())
            .Returns(storefront);

        _tenantStore.GetActiveMembershipAsync(
                tenantId,
                userSub,
                Arg.Any<CancellationToken>())
            .Returns(TenantMembership.Create(
                tenantId,
                userSub,
                TenantMembershipRoles.Admin));

        var nextCalled = false;
        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.True(nextCalled);

        var resolved = context.Items["__TenantContext"] as ITenantContext;
        Assert.NotNull(resolved);
        Assert.Equal(tenantId, resolved.TenantId);
    }

    [Fact]
    public async Task Admin_Route_Returns_403_When_User_Is_Not_A_Member_Of_Host_Tenant()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/products";
        context.Request.Host = new HostString("admin.example.com");

        const string userSub = "auth0|unauthorized-user";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userSub)],
                "Bearer"));

        TenantId tenantId = TenantId.New();

        var storefront = Storefront.Create(
            StorefrontId.New(),
            tenantId,
            "admin.example.com",
            MarketId.From("AZ"),
            "az-AZ");

        _tenantStore.GetStorefrontByHostAsync(
                "admin.example.com",
                Arg.Any<CancellationToken>())
            .Returns(storefront);

        _tenantStore.GetActiveMembershipAsync(
                tenantId,
                userSub,
                Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        var nextCalled = false;
        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Catalog_Route_Returns_403_When_User_Is_Not_A_Member_Of_Host_Tenant()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("store1.example.com");

        const string userSub = "auth0|unauthorized-user";
        context.User = new ClaimsPrincipal(
            new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userSub)],
                "Bearer"));

        TenantId tenantId = TenantId.New();

        _tenantStore.GetStorefrontByHostAsync(
                "store1.example.com",
                Arg.Any<CancellationToken>())
            .Returns(Storefront.Create(
                StorefrontId.New(),
                tenantId,
                "store1.example.com",
                MarketId.From("AZ"),
                "az-AZ"));

        _tenantStore.GetActiveMembershipAsync(
                tenantId,
                userSub,
                Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        var nextCalled = false;
        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Api_Route_Returns_400_When_Tenant_Cannot_Be_Resolved()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("unknown.example.com");

        _tenantStore.GetStorefrontByHostAsync(
                "unknown.example.com",
                Arg.Any<CancellationToken>())
            .Returns((Storefront?)null);

        var nextCalled = false;

        var middleware = CreatePipeline(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
    }

    [Fact]
    public async Task Resolution_Alone_Does_Not_Require_Membership()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("store.example.com");

        TenantId tenantId = TenantId.New();

        _tenantStore.GetStorefrontByHostAsync(
                "store.example.com",
                Arg.Any<CancellationToken>())
            .Returns(Storefront.Create(
                StorefrontId.New(),
                tenantId,
                "store.example.com",
                MarketId.From("AZ"),
                "az-AZ"));

        bool nextCalled = false;

        var middleware = new TenantResolutionMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.True(nextCalled);
        Assert.True(HttpTenantContext.HasResolvedTenant(context));

        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Pipeline_Rejects_Unauthenticated_Identity(
        bool includeSubjectClaim)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("store.example.com");

        // A claim does not make an identity authenticated.
        var identity = new ClaimsIdentity();

        if (includeSubjectClaim)
        {
            identity.AddClaim(new Claim("sub", "untrusted-user"));
        }

        context.User = new ClaimsPrincipal(identity);

        _tenantStore.GetStorefrontByHostAsync(
                "store.example.com",
                Arg.Any<CancellationToken>())
            .Returns(Storefront.Create(
                StorefrontId.New(),
                TenantId.New(),
                "store.example.com",
                MarketId.From("AZ"),
                "az-AZ"));

        bool nextCalled = false;

        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.False(nextCalled);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            context.Response.StatusCode);

        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membership_Rejects_Missing_Tenant_Context()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";

        bool nextCalled = false;

        var middleware = new TenantMembershipMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(
                context,
                _tenantStore,
                TenantContext.Empty));

        Assert.False(nextCalled);
    }

    [Fact]
    public async Task Pipeline_Rejects_Inactive_Storefront()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/products";
        context.Request.Host = new HostString("store.example.com");

        var storefront = Storefront.Create(
            StorefrontId.New(),
            TenantId.New(),
            "store.example.com",
            MarketId.From("AZ"),
            "az-AZ");

        storefront.Deactivate();

        _tenantStore.GetStorefrontByHostAsync(
                "store.example.com",
                Arg.Any<CancellationToken>())
            .Returns(storefront);

        bool nextCalled = false;

        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.False(nextCalled);
        Assert.False(HttpTenantContext.HasResolvedTenant(context));
        Assert.Equal(
            StatusCodes.Status400BadRequest,
            context.Response.StatusCode);

        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Pipeline_Skips_NonApi_Routes()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/health/live";

        bool nextCalled = false;

        var middleware = CreatePipeline(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(context, _tenantStore);

        Assert.True(nextCalled);

        await _tenantStore.DidNotReceive().GetStorefrontByHostAsync(
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());

        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Membership_DoesNotSkip_ForAllowAnonymousAlone()
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/storefront/products";
        context.Request.Method = HttpMethods.Get;
        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(new AllowAnonymousAttribute()),
            "anonymous-without-storefront-metadata"));

        bool nextCalled = false;
        var middleware = new TenantMembershipMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await middleware.InvokeAsync(
            context,
            _tenantStore,
            TenantContext.ForTenant(TenantId.New()));

        Assert.False(nextCalled);
        Assert.Equal(
            StatusCodes.Status401Unauthorized,
            context.Response.StatusCode);
    }

    [Theory]
    [InlineData("POST", true)]
    [InlineData("DELETE", true)]
    [InlineData("GET", false)]
    public async Task Membership_Rejects_InvalidPublicMetadata(
        string method,
        bool allowAnonymous)
    {
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/storefront/products";
        context.Request.Method = method;

        var metadata = new List<object> { PublicStorefrontReadMetadata.Instance };
        if (allowAnonymous)
        {
            metadata.Add(new AllowAnonymousAttribute());
        }

        context.SetEndpoint(new Endpoint(
            _ => Task.CompletedTask,
            new EndpointMetadataCollection(metadata),
            "invalid-public-endpoint"));

        bool nextCalled = false;
        var middleware = new TenantMembershipMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            middleware.InvokeAsync(
                context,
                _tenantStore,
                TenantContext.ForTenant(TenantId.New())));

        Assert.False(nextCalled);
        await _tenantStore.DidNotReceive().GetActiveMembershipAsync(
            Arg.Any<TenantId>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    private TenantResolutionMiddleware CreatePipeline(RequestDelegate next)
    {
        var membershipMiddleware = new TenantMembershipMiddleware(next);

        return new TenantResolutionMiddleware(context =>
        {
            ITenantContext tenantContext =
                context.Items["__TenantContext"] as ITenantContext ??
                TenantContext.Empty;

            return membershipMiddleware.InvokeAsync(
                context,
                _tenantStore,
                tenantContext);
        });
    }
}
