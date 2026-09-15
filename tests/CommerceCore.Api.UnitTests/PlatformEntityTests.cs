using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;

namespace CommerceCore.Api.UnitTests;

public sealed class PlatformEntityTests
{
    [Fact]
    public void Tenant_Create_Normalizes_Values_And_Controls_Status()
    {
        TenantId tenantId = TenantId.New();
        var tenant = Tenant.Create(tenantId, "  TENANT-SLUG  ", "  Tenant Name  ");

        Assert.Equal(tenantId, tenant.Id);
        Assert.Equal("tenant-slug", tenant.Slug);
        Assert.Equal("Tenant Name", tenant.Name);
        Assert.Equal(TenantStatuses.Active, tenant.Status);

        tenant.Deactivate();
        Assert.Equal(TenantStatuses.Inactive, tenant.Status);

        tenant.Activate();
        Assert.Equal(TenantStatuses.Active, tenant.Status);

        Assert.Throws<ArgumentException>(() => Tenant.Create(default, "slug", "name"));
        Assert.Throws<ArgumentException>(() => Tenant.Create(tenantId, "slug", "name", default(DateTime)));
    }

    [Fact]
    public void TenantMembership_Create_Normalizes_Subject_And_Controls_Status()
    {
        TenantId tenantId = TenantId.New();
        var membership = TenantMembership.Create(tenantId, "  auth0|user-123  ", "  Admin  ");

        Assert.Equal(tenantId, membership.TenantId);
        Assert.Equal("auth0|user-123", membership.UserSubject);
        Assert.Equal("Admin", membership.Role);
        Assert.Equal(TenantMembershipStatuses.Active, membership.Status);

        membership.Deactivate();
        Assert.Equal(TenantMembershipStatuses.Inactive, membership.Status);

        membership.Activate();
        Assert.Equal(TenantMembershipStatuses.Active, membership.Status);

        Assert.Throws<ArgumentException>(() => TenantMembership.Create(default, "user", "Admin"));
    }

    [Fact]
    public void Storefront_Create_Normalizes_Host_And_Controls_Status()
    {
        StorefrontId storefrontId = StorefrontId.New();
        TenantId tenantId = TenantId.New();
        MarketId marketId = MarketId.From("AZ");

        var storefront = Storefront.Create(
            storefrontId,
            tenantId,
            " Store.Example.COM. ",
            marketId,
            "  az-AZ  ");

        Assert.Equal(storefrontId.Value, storefront.Id);
        Assert.Equal(tenantId, storefront.TenantId);
        Assert.Equal("store.example.com", storefront.HostName);
        Assert.Equal("AZ", storefront.MarketCode);
        Assert.Equal("az-AZ", storefront.DefaultLocale);
        Assert.True(storefront.IsActive);

        storefront.Deactivate();
        Assert.False(storefront.IsActive);

        storefront.Activate();
        Assert.True(storefront.IsActive);

        Assert.Throws<ArgumentException>(() => Storefront.Create(default, tenantId, "store.example.com", marketId));
        Assert.Throws<ArgumentException>(() => Storefront.Create(storefrontId, default, "store.example.com", marketId));
        Assert.Throws<ArgumentException>(() => Storefront.Create(storefrontId, tenantId, "store.example.com", default));
        Assert.Throws<ArgumentException>(() => Storefront.Create(storefrontId, tenantId, "invalid..host@@", marketId));
    }
}
