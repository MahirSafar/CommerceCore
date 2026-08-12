namespace CommerceCore.Domain.Catalog.Products.ValueObjects;

public readonly record struct ProductId
{
    private ProductId(Guid value)
    {
        Value = value;
    }

    public Guid Value { get; }

    public static ProductId New()
        => new(Guid.CreateVersion7());

    public static ProductId From(Guid value)
    {
        if (value == Guid.Empty)
            throw new ArgumentException(
                "Product ID cannot be empty.",
                nameof(value));

        return new ProductId(value);
    }

    public override string ToString() => Value.ToString();
}