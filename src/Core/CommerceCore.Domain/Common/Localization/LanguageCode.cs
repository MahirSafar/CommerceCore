using System.Text.RegularExpressions;

namespace CommerceCore.Domain.Common.Localization;

public readonly partial record struct LanguageCode
{
    private readonly string? _value;
    public string Value => _value ?? throw new InvalidOperationException("LanguageCode has not been initialized. Always use the Create() method.");

    private LanguageCode(string value) => _value = value;

    public static LanguageCode Create(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, nameof(value));

        var code = value.Trim().ToLowerInvariant();

        return !LanguageTagRegex().IsMatch(code)
            ? throw new ArgumentException($"Invalid language code format: '{value}'. Only BCP-47 tags are allowed (e.g., az, en, pt-BR).", nameof(value))
            : new LanguageCode(code);
    }
    public override string ToString() => _value ?? string.Empty;

    [GeneratedRegex(@"^[a-z]{2,3}(-[a-z0-9]{2,8})?$")]
    private static partial Regex LanguageTagRegex();
}
