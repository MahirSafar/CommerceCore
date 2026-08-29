using System.Security.Claims;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using CommerceCore.Platform.ControlPlane.Entities;
using CommerceCore.Platform.Identity;
using Microsoft.AspNetCore.Http;
using NSubstitute;

namespace CommerceCore.Api.UnitTests;

public sealed class TenantResolutionMiddlewareTests
{
    private readonly IPlatformTenantStore _tenantStore = Substitute.For<IPlatformTenantStore>();

    [Fact]
    public async Task Storefront_Route_Resolves_Tenant_From_Host()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/storefront/catalog/products";
        context.Request.Host = new HostString("store1.example.com");

        var tenantId = Guid.NewGuid();
        var storefrontId = Guid.NewGuid();
        var storefront = new Storefront
        {
            Id = storefrontId,
            TenantId = tenantId,
            HostName = "store1.example.com",
            MarketCode = "AZ",
            DefaultLocale = "az-AZ",
            IsActive = true
        };

        _tenantStore.GetStorefrontByHostAsync("store1.example.com", Arg.Any<CancellationToken>())
            .Returns(storefront);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(ctx =>
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
        var middleware = new TenantResolutionMiddleware(ctx =>
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
    public async Task Admin_Route_Resolves_Tenant_From_Sub_Claim_Membership()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/catalog/products";

        var userSub = "auth0|admin-user-123";
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userSub) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        var tenantId = Guid.NewGuid();
        var membership = new TenantMembership
        {
            TenantId = tenantId,
            UserSubject = userSub,
            Role = "Admin",
            Status = "Active"
        };

        _tenantStore.GetMembershipByUserSubjectAsync(userSub, Arg.Any<CancellationToken>())
            .Returns(membership);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(ctx =>
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
    }

    [Fact]
    public async Task Admin_Route_Returns_403_When_No_Active_Membership()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/admin/products";

        var userSub = "auth0|unauthorized-user";
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, userSub) };
        context.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

        _tenantStore.GetMembershipByUserSubjectAsync(userSub, Arg.Any<CancellationToken>())
            .Returns((TenantMembership?)null);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(ctx =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        // Act
        await middleware.InvokeAsync(context, _tenantStore);

        // Assert
        Assert.False(nextCalled);
        Assert.Equal(StatusCodes.Status403Forbidden, context.Response.StatusCode);
    }

    [Fact]
    public async Task Partner_Route_Resolves_Tenant_From_Client_Id()
    {
        // Arrange
        var context = new DefaultHttpContext();
        context.Request.Path = "/api/partner/products";
        context.Request.Headers["X-Partner-Client-Id"] = "partner-erp-client";

        var tenantId = Guid.NewGuid();
        var tenant = new Tenant
        {
            Id = tenantId,
            Slug = "partner-erp-client",
            Name = "Partner Tenant",
            Status = "Active"
        };

        _tenantStore.GetTenantByPartnerClientIdAsync("partner-erp-client", Arg.Any<CancellationToken>())
            .Returns(tenant);

        var nextCalled = false;
        var middleware = new TenantResolutionMiddleware(ctx =>
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
    }
}
