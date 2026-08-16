using System.Collections.ObjectModel;

namespace CommerceCore.Domain.Catalog.Attributes.ValueObjects;

public abstract record AttributeValue
{
    public sealed record Text : AttributeValue
    {
        private Text(string value) => Value = value;

        public string Value { get; }

        public static Text Create(string value)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);

            return new Text(value.Trim());
        }
    }

    public sealed record Integer : AttributeValue
    {
        private Integer(long value) => Value = value;

        public long Value { get; }

        public static Integer Create(long value) => new(value);
    }

    public sealed record Decimal : AttributeValue
    {
        private Decimal(decimal value) => Value = value;

        public decimal Value { get; }

        public static Decimal Create(decimal value) => new(value);
    }

    public sealed record Boolean : AttributeValue
    {
        private Boolean(bool value) => Value = value;

        public bool Value { get; }

        public static Boolean Create(bool value) => new(value);
    }

    public sealed record SingleSelect : AttributeValue
    {
        private SingleSelect(string optionCode) => OptionCode = optionCode;

        public string OptionCode { get; }

        public static SingleSelect Create(string optionCode)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);

            return new SingleSelect(optionCode.Trim());
        }
    }

    public sealed record MultiSelect : AttributeValue
    {
        private readonly ReadOnlyCollection<string> _optionCodes;

        private MultiSelect(List<string> optionCodes) =>
            _optionCodes = optionCodes.AsReadOnly();

        public IReadOnlyList<string> OptionCodes => _optionCodes;

        public static MultiSelect Create(IEnumerable<string> optionCodes)
        {
            ArgumentNullException.ThrowIfNull(optionCodes);

            List<string> normalized = [.. optionCodes
                .Select(NormalizeOptionCode)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(code => code, StringComparer.Ordinal)];

            return normalized.Count == 0
                ? throw new ArgumentException("At least one option code is required.", nameof(optionCodes))
                : new MultiSelect(normalized);
        }

        public bool Equals(MultiSelect? other)
            => other is not null &&
               OptionCodes.SequenceEqual(
                   other.OptionCodes,
                   StringComparer.Ordinal);

        public override int GetHashCode()
        {
            HashCode hash = new();

            foreach (string optionCode in OptionCodes)
                hash.Add(optionCode, StringComparer.Ordinal);

            return hash.ToHashCode();
        }
    }

    public sealed record Measurement : AttributeValue
    {
        private Measurement(
            decimal value,
            string unit,
            decimal canonicalValue,
            string canonicalUnit)
        {
            Value = value;
            Unit = unit;
            CanonicalValue = canonicalValue;
            CanonicalUnit = canonicalUnit;
        }

        public decimal Value { get; }
        public string Unit { get; }
        public decimal CanonicalValue { get; }
        public string CanonicalUnit { get; }

        public static Measurement Create(
            decimal value,
            string unit,
            decimal canonicalValue,
            string canonicalUnit)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(unit);
            ArgumentException.ThrowIfNullOrWhiteSpace(canonicalUnit);

            return new Measurement(
                value,
                unit.Trim(),
                canonicalValue,
                canonicalUnit.Trim());
        }
    }

    private static string NormalizeOptionCode(string optionCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(optionCode);

        return optionCode.Trim();
    }
}