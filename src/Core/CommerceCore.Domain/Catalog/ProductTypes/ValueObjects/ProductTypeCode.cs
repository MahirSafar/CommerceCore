using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly partial record struct ProductTypeCode
{
    public const int MaximumLength = 64;

    private readonly string? _value;

    private ProductTypeCode(string value) => _value = value;

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "An uninitialized product type code cannot be used.");

    public static ProductTypeCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim();

        if (normalized.Length > MaximumLength ||
            !ProductTypeCodePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Product type code must be lowercase snake_case, start with a letter, " +
                $"end with a letter or digit, and be at most {MaximumLength} characters.",
                nameof(value));
        }

        return new ProductTypeCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z](?:[a-z0-9_]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex ProductTypeCodePattern();
}