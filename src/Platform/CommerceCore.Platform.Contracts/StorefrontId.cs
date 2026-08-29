namespace CommerceCore.Platform.Contracts;

public readonly record struct StorefrontId
{
    private StorefrontId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static StorefrontId New() => new(Guid.CreateVersion7());

    public static StorefrontId From(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Storefront ID cannot be empty.", nameof(value));
        }

        return new StorefrontId(value);
    }

    public static bool TryParse(string? input, out StorefrontId storefrontId)
    {
        if (Guid.TryParse(input, out var guid) && guid != Guid.Empty)
        {
            storefrontId = new StorefrontId(guid);
            return true;
        }

        storefrontId = default;
        return false;
    }

    public override string ToString() => Value.ToString();

    public static implicit operator Guid(StorefrontId id) => id.Value;
}
