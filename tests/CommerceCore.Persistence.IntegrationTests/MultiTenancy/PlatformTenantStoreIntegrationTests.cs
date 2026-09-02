using CommerceCore.Persistence.ControlPlane;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

[Collection(nameof(PostgreSqlCollection))]
public sealed class PlatformTenantStoreIntegrationTests
{
    private readonly PostgreSqlFixture _fixture;

    public PlatformTenantStoreIntegrationTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task GetActiveMembershipAsync_ReturnsMembership_WhenTenantAndMembershipAreActive_AndReturnsNull_WhenTenantIsInactive()
    {
        CancellationToken cancellationToken =
            TestContext.Current.CancellationToken;

        TenantId tenantId = await _fixture.CreateTenantAsync(cancellationToken);
        string userSubject = $"auth0|user-{Guid.NewGuid():N}";

        await _fixture.CreateMembershipAsync(
            tenantId,
            userSubject,
            TenantMembershipRoles.Admin,
            TenantMembershipStatuses.Active,
            cancellationToken);

        await using var scope = _fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CommerceCoreDbContext>();
        var store = new PlatformTenantStore(db);

        var activeMembership = await store.GetActiveMembershipAsync(
            tenantId,
            userSubject,
            cancellationToken);

        Assert.NotNull(activeMembership);
        Assert.Equal(tenantId, activeMembership.TenantId);
        Assert.Equal(userSubject, activeMembership.UserSubject);
        Assert.Equal(TenantMembershipStatuses.Active, activeMembership.Status);

        await _fixture.SetTenantStatusAsync(
            tenantId,
            TenantStatuses.Inactive,
            cancellationToken);

        var inactiveTenantMembership = await store.GetActiveMembershipAsync(
            tenantId,
            userSubject,
            cancellationToken);

        Assert.Null(inactiveTenantMembership);
    }
}
