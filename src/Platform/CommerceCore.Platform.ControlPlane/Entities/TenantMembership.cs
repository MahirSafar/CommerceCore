using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class TenantMembership
{
    public TenantId TenantId { get; set; }
    public string UserSubject { get; set; } = string.Empty;
    public string Role { get; set; } = "Admin";
    public string Status { get; set; } = "Active";
}
