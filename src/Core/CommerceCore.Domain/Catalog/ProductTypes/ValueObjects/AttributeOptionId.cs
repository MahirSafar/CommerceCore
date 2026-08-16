namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly record struct AttributeOptionId
{
    private AttributeOptionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static AttributeOptionId New() => new(Guid.CreateVersion7());

    public static AttributeOptionId From(Guid value) => value == Guid.Empty
            ? throw new ArgumentException("Attribute option ID cannot be empty.", nameof(value))
            : new AttributeOptionId(value);

    public override string ToString() => Value.ToString();
}