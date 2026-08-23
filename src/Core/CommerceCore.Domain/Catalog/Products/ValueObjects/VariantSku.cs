namespace CommerceCore.Domain.Catalog.Products.ValueObjects;

public readonly record struct VariantSku
{
    public const int MaximumLength = 128;

    private VariantSku(string value) => Value = value;

    public string Value { get; }

    public static VariantSku Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim().ToUpperInvariant();

        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                $"Variant SKU cannot exceed {MaximumLength} characters.");
        }

        if (!normalized.All(IsAllowedCharacter))
        {
            throw new ArgumentException(
                "Variant SKU can contain only ASCII letters, digits, '-', '_' and '.'.",
                nameof(value));
        }

        return new VariantSku(normalized);
    }

    public override string ToString() => Value;

    private static bool IsAllowedCharacter(char character) =>
        character is >= 'A' and <= 'Z' or
        >= '0' and <= '9' or
        '-' or '_' or '.';
}