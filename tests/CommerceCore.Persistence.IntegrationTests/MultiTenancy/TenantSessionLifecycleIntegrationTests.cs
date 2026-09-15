using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

[Collection(nameof(PostgreSqlCollection))]
public sealed class TenantSessionLifecycleIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public TenantSessionLifecycleIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Catalog_Write_After_Platform_Resolution_Uses_Resolved_Tenant()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        // 1. Seed unique host and membership for primary tenant
        TenantId primaryTenantId = _fixture.PrimaryTenantId;
        string hostName = $"shop-{Guid.NewGuid():N}.example.test";
        string userSubject = $"auth0|user-{Guid.NewGuid():N}";

        await _fixture.CreateStorefrontAsync(
            primaryTenantId,
            hostName,
            cancellationToken);

        await _fixture.CreateMembershipAsync(
            primaryTenantId,
            userSubject,
            TenantMembershipRoles.Admin,
            TenantMembershipStatuses.Active,
            cancellationToken);

        // 2. In a new DI scope, read storefront first, then membership with empty tenant context
        var tenantContext = _fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.Clear();

        await using var scope = _fixture.Services.CreateAsyncScope();
        var tenantStore = scope.ServiceProvider.GetRequiredService<IPlatformTenantStore>();

        var storefront = await tenantStore.GetStorefrontByHostAsync(
            hostName,
            cancellationToken);

        Assert.NotNull(storefront);
        Assert.Equal(primaryTenantId, storefront.TenantId);

        var membership = await tenantStore.GetActiveMembershipAsync(
            storefront.TenantId,
            userSubject,
            cancellationToken);

        Assert.NotNull(membership);
        Assert.Equal(primaryTenantId, membership.TenantId);

        // 3. Set tenant on TestTenantContext
        tenantContext.SetTenant(primaryTenantId);

        // 4. From the same scope, get CommerceCoreDbContext, add ProductType.CreateRoot and SaveChangesAsync
        var db = scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();
        var productTypeCode = ProductTypeCode.Create($"type_{Guid.NewGuid():N}");
        var productType = ProductType.CreateRoot(
            primaryTenantId,
            productTypeCode,
            isAssignable: true);

        db.ProductTypes.Add(productType);
        await db.SaveChangesAsync(cancellationToken);

        // 5. Read back product type and assert TenantId is primary tenant
        var savedProductType = await db.ProductTypes
            .FirstOrDefaultAsync(
                pt => pt.Id == productType.Id,
                cancellationToken);

        Assert.NotNull(savedProductType);
        Assert.Equal(primaryTenantId, savedProductType.TenantId);
    }
}
