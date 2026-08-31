using CommerceCore.Domain.Common.Interfaces;

namespace CommerceCore.Domain.Common.Entities;

public abstract class SoftDeletableAggregateRoot<TKey>
  : AggregateRoot<TKey>, ISoftDeletable
  where TKey : notnull
{
    protected SoftDeletableAggregateRoot()
    {
    }

    protected SoftDeletableAggregateRoot(TKey id)
      : base(id)
    {
    }

    public bool IsDeleted { get; private set; }
    public DateTimeOffset? DeletedAtUtc { get; private set; }
    public string? DeletedBy { get; private set; }

    protected bool MarkAsDeletedCore(
      DateTimeOffset deletedAtUtc,
      string? deletedBy)
    {
        ArgumentOutOfRangeException.ThrowIfEqual(
          deletedAtUtc,
          DateTimeOffset.MinValue);

        if (IsDeleted)
            return false;

        IsDeleted = true;
        DeletedAtUtc = deletedAtUtc.ToUniversalTime();
        DeletedBy = deletedBy;

        return true;
    }

    protected bool RestoreCore()
    {
        if (!IsDeleted)
            return false;

        IsDeleted = false;
        DeletedAtUtc = null;
        DeletedBy = null;

        return true;
    }
}
