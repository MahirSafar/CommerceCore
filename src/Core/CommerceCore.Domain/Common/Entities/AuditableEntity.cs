using CommerceCore.Domain.Common.Interfaces;

namespace CommerceCore.Domain.Common.Entities;

public abstract class AuditableEntity<TKey> : BaseEntity<TKey>, IAuditableEntity
  where TKey : notnull
{
    protected AuditableEntity()
    {
    }

    protected AuditableEntity(TKey id)
      : base(id)
    {
    }

    public DateTimeOffset CreatedAtUtc { get; private set; }
    public string? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAtUtc { get; private set; }
    public string? UpdatedBy { get; private set; }
}
