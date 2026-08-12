using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Common.Localization;

public readonly partial record struct LanguageCode
{
    private readonly string? _value;

    private LanguageCode(string value)
    {
        _value = value;
    }

    public string Value
        => _value ?? throw new InvalidOperationException(
            "LanguageCode is not initialized. Use Create().");

    public static LanguageCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var trimmed = value.Trim();

        if (!LanguageTagRegex().IsMatch(trimmed))
        {
            throw new ArgumentException(
                $"'{value}' is not a supported language tag.",
                nameof(value));
        }

        return new LanguageCode(Normalize(trimmed));
    }

    public override string ToString() => Value;

    private static string Normalize(string value)
    {
        var parts = value.Split('-', StringSplitOptions.RemoveEmptyEntries);

        for (var index = 0; index < parts.Length; index++)
        {
            parts[index] = index switch
            {
                0 => parts[index].ToLowerInvariant(),
                _ when parts[index].Length == 2 &&
                       parts[index].All(char.IsLetter)
                    => parts[index].ToUpperInvariant(),
                _ when parts[index].Length == 4 &&
                       parts[index].All(char.IsLetter)
                    => char.ToUpperInvariant(parts[index][0]) +
                       parts[index][1..].ToLowerInvariant(),
                _ => parts[index].ToLowerInvariant()
            };
        }

        return string.Join("-", parts);
    }

    [GeneratedRegex(
        @"^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$",
        RegexOptions.CultureInvariant)]
    private static partial Regex LanguageTagRegex();
}