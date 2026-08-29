namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly record struct ProductTypeId
{
    private ProductTypeId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ProductTypeId New() => new(Guid.CreateVersion7());

    public static ProductTypeId From(Guid value) => value == Guid.Empty
            ? throw new ArgumentException("Product type ID cannot be empty.", nameof(value))
            : new ProductTypeId(value);

    public override string ToString() => Value.ToString();
}