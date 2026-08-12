namespace CommerceCore.Domain.Common.Entities;

public abstract class BaseEntity<TKey> : IEquatable<BaseEntity<TKey>>
  where TKey : notnull
{
    protected BaseEntity()
    {
    }

    protected BaseEntity(TKey id)
    {
        ArgumentNullException.ThrowIfNull(id);

        if (IsTransient(id))
            throw new ArgumentException(
              "Entity ID cannot be the default value.",
              nameof(id));

        Id = id;
    }

    public TKey Id { get; private set; } = default!;

    public bool Equals(BaseEntity<TKey>? other)
    {
        if (other is null)
            return false;

        if (ReferenceEquals(this, other))
            return true;

        if (GetType() != other.GetType())
            return false;

        if (IsTransient(Id) || IsTransient(other.Id))
            return false;

        return EqualityComparer<TKey>.Default.Equals(Id, other.Id);
    }

    public override bool Equals(object? obj)
      => obj is BaseEntity<TKey> other && Equals(other);

    public override int GetHashCode()
      => IsTransient(Id)
        ? base.GetHashCode()
        : HashCode.Combine(GetType(), Id);

    public static bool operator ==(BaseEntity<TKey>? left, BaseEntity<TKey>? right)
      => left?.Equals(right) ?? right is null;

    public static bool operator !=(BaseEntity<TKey>? left, BaseEntity<TKey>? right)
      => !(left == right);

    private static bool IsTransient(TKey id)
      => EqualityComparer<TKey>.Default.Equals(id, default!);
}
