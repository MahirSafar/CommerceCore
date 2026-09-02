using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class TenantMembership
{
    public TenantId TenantId { get; set; }
    public string UserSubject { get; set; } = string.Empty;
    public string Role { get; set; } = TenantMembershipRoles.Admin;
    public string Status { get; set; } = TenantMembershipStatuses.Active;
}
