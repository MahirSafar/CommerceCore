using CommerceCore.Platform.Contracts;

namespace CommerceCore.Platform.ControlPlane.Entities;

public sealed class Tenant
{
    private Tenant()
    {
    }

    public TenantId Id { get; private set; }
    public string Slug { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public string Status { get; private set; } = TenantStatuses.Active;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public static Tenant Create(
        TenantId id,
        string slug,
        string name,
        DateTime? createdAtUtc = null)
    {
        if (id.Value == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        if (createdAtUtc.HasValue && createdAtUtc.Value == default)
        {
            throw new ArgumentException("Created timestamp cannot be default.", nameof(createdAtUtc));
        }

        return new Tenant
        {
            Id = id,
            Slug = slug.Trim().ToLowerInvariant(),
            Name = name.Trim(),
            Status = TenantStatuses.Active,
            CreatedAtUtc = createdAtUtc ?? DateTime.UtcNow
        };
    }

    public void Activate()
    {
        Status = TenantStatuses.Active;
    }

    public void Deactivate()
    {
        Status = TenantStatuses.Inactive;
    }
}
