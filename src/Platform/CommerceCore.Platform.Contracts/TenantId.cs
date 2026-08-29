namespace CommerceCore.Platform.Contracts;

public readonly record struct TenantId
{
    private TenantId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static TenantId New() => new(Guid.CreateVersion7());

    public static TenantId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Tenant ID cannot be empty.", nameof(value));
        }

        return new TenantId(value);
    }

    public static bool TryParse(string? input, out TenantId tenantId)
    {
        if (Guid.TryParse(input, out var guid) && guid != Guid.Empty)
        {
            tenantId = new TenantId(guid);
            return true;
        }

        tenantId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(TenantId id) => id.Value;
}
