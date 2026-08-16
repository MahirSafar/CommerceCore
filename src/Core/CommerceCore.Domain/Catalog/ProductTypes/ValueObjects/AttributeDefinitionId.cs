namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly record struct AttributeDefinitionId
{
    private AttributeDefinitionId(Guid value) => Value = value;

    public Guid Value { get; }

    public static AttributeDefinitionId New() => new(Guid.CreateVersion7());

    public static AttributeDefinitionId From(Guid value) => value == Guid.Empty
            ? throw new ArgumentException("Attribute definition ID cannot be empty.", nameof(value))
            : new AttributeDefinitionId(value);

    public override string ToString() => Value.ToString();
}