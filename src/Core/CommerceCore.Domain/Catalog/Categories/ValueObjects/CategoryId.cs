namespace CommerceCore.Domain.Catalog.Categories.ValueObjects;

public readonly record struct CategoryId
{
    private CategoryId(Guid value) => Value = value;

    public Guid Value { get; }

    public static CategoryId New() => new(Guid.CreateVersion7());

    public static CategoryId From(Guid value) => value == Guid.Empty
            ? throw new ArgumentException("Category ID cannot be empty.", nameof(value))
            : new CategoryId(value);

    public override string ToString() => Value.ToString();
}