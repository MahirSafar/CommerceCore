using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane.Entities;

namespace CommerceCore.Platform.ControlPlane;

public interface IPlatformTenantStore
{
    Task<Tenant?> GetTenantByIdAsync(TenantId tenantId, CancellationToken cancellationToken = default);
    Task<Storefront?> GetStorefrontByHostAsync(string hostName, CancellationToken cancellationToken = default);
    Task<TenantMembership?> GetMembershipByUserSubjectAsync(string userSubject, CancellationToken cancellationToken = default);
    Task<Tenant?> GetTenantByPartnerClientIdAsync(string clientId, CancellationToken cancellationToken = default);
}
