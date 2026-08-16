using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly partial record struct AttributeOptionCode
{
    public const int MaximumLength = 64;

    private readonly string? _value;

    private AttributeOptionCode(string value) => _value = value;


    public string Value =>
        _value ?? throw new InvalidOperationException(
            "An uninitialized attribute option code cannot be used.");

    public static AttributeOptionCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim();

        if (normalized.Length > MaximumLength)
        {
            throw new ArgumentException(
                $"Attribute option code cannot exceed {MaximumLength} characters.",
                nameof(value));
        }

        if (!OptionCodePattern().IsMatch(normalized))
        {
            throw new ArgumentException(
                "Attribute option code must be lowercase kebab-case, such as " +
                "'space-black', 'medium', or '250g'.",
                nameof(value));
        }

        return new AttributeOptionCode(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex OptionCodePattern();
}