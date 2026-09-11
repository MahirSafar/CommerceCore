using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class TenantMembership
{
    private TenantMembership()
    {
    }

    public TenantId TenantId { get; private set; }
    public string UserSubject { get; private set; } = string.Empty;
    public string Role { get; private set; } = TenantMembershipRoles.Admin;
    public string Status { get; private set; } = TenantMembershipStatuses.Active;

    public static TenantMembership Create(
        TenantId tenantId,
        string userSubject,
        string role = TenantMembershipRoles.Admin)
    {
        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(tenantId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(userSubject);
        ArgumentException.ThrowIfNullOrWhiteSpace(role);

        return new TenantMembership
        {
            TenantId = tenantId,
            UserSubject = userSubject.Trim(),
            Role = role.Trim(),
            Status = TenantMembershipStatuses.Active
        };
    }

    public void Activate()
    {
        Status = TenantMembershipStatuses.Active;
    }

    public void Deactivate()
    {
        Status = TenantMembershipStatuses.Inactive;
    }
}
