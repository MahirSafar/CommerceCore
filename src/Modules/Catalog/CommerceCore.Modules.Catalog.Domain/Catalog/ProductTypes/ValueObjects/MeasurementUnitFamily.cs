using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

public readonly partial record struct MeasurementUnitFamily
{
    public const int MaximumLength = 64;

    private readonly string? _value;

    private MeasurementUnitFamily(string value) => _value = value;

    public string Value =>
        _value ?? throw new InvalidOperationException(
            "An uninitialized measurement unit family cannot be used.");

    public static MeasurementUnitFamily Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        string normalized = value.Trim();

        return normalized.Length > MaximumLength || !UnitFamilyPattern().IsMatch(normalized)
            ? throw new ArgumentException(
                "Measurement unit family must be lowercase snake_case and at most 64 characters.", nameof(value))
            : new MeasurementUnitFamily(normalized);
    }

    public override string ToString() => Value;

    [GeneratedRegex(@"^[a-z](?:[a-z0-9_]{0,62}[a-z0-9])?$", RegexOptions.CultureInvariant)]
    private static partial Regex UnitFamilyPattern();
}