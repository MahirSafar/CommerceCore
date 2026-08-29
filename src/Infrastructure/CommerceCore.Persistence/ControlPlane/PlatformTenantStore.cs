using CommerceCore.Platform.Contracts;
using CommerceCore.Platform.ControlPlane;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence.ControlPlane;

public sealed class PlatformTenantStore : IPlatformTenantStore
{
    private readonly CommerceCoreDbContext _dbContext;

    public PlatformTenantStore(CommerceCoreDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Tenant?> GetTenantByIdAsync(TenantId tenantId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == tenantId.Value, cancellationToken);
    }

    public async Task<Storefront?> GetStorefrontByHostAsync(string hostName, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(hostName))
            return null;

        var normalizedHost = hostName.Trim().ToLowerInvariant();

        return await _dbContext.Set<Storefront>()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.HostName.ToLower() == normalizedHost, cancellationToken);
    }

    public async Task<TenantMembership?> GetMembershipByUserSubjectAsync(string userSubject, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userSubject))
            return null;

        return await _dbContext.Set<TenantMembership>()
            .AsNoTracking()
            .FirstOrDefaultAsync(m => m.UserSubject == userSubject && m.Status == "Active", cancellationToken);
    }

    public async Task<Tenant?> GetTenantByPartnerClientIdAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId))
            return null;

        // Partner client id mapping: slug or direct match
        return await _dbContext.Set<Tenant>()
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Slug == clientId, cancellationToken);
    }
}
