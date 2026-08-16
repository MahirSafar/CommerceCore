namespace CommerceCore.Domain.Catalog.ProductVariants.ValueObjects;

public readonly record struct ProductVariantId
{
    private ProductVariantId(Guid value) => Value = value;

    public Guid Value { get; }

    public static ProductVariantId New() => new(Guid.CreateVersion7());

    public static ProductVariantId From(Guid value) => value == Guid.Empty
            ? throw new ArgumentException("Product variant ID cannot be empty.", nameof(value))
            : new ProductVariantId(value);

    public override string ToString() => Value.ToString();
}