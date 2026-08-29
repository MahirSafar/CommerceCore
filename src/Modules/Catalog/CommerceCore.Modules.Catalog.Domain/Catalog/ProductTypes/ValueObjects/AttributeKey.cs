using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly partial record struct AttributeKey
{
    public const int MaximumLength = 64;

    private readonly string? _value;

    private AttributeKey(string value) => _value = value;

    public string Value => _value ?? throw new InvalidOperationException(
        "AttributeKey is not initialized. Use Create().");

    public static AttributeKey Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        if (!StringComparer.Ordinal.Equals(value, value.Trim()))
            throw new ArgumentException("Attribute key cannot have leading or trailing whitespace.", nameof(value));

        return value.Length > MaximumLength || !AttributeKeyRegex().IsMatch(value)
            ? throw new ArgumentException(
                "Attribute key must be lowercase snake_case, start with a letter, " +
                $"end with a letter or digit, and be at most {MaximumLength} characters.",
                nameof(value))
            : new AttributeKey(value);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z](?:[a-z0-9_]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex AttributeKeyRegex();
}