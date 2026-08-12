using CommerceCore.Domain.Common.Localization;
using System.Collections.ObjectModel;

namespace CommerceCore.Domain.Common.ValueObjects.Localization;

public sealed class LocalizedText : IEquatable<LocalizedText>
{
    private readonly ReadOnlyDictionary<LanguageCode, string> _translations;

    private LocalizedText(
        LanguageCode defaultLanguage,
        Dictionary<LanguageCode, string> translations)
    {
        DefaultLanguage = defaultLanguage;
        _translations = new ReadOnlyDictionary<LanguageCode, string>(
            translations);
    }

    public LanguageCode DefaultLanguage { get; }

    public IReadOnlyDictionary<LanguageCode, string> Translations
        => _translations;

    public static LocalizedText Create(
        LanguageCode defaultLanguage,
        IEnumerable<KeyValuePair<LanguageCode, string>> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        if (defaultLanguage == default)
        {
            throw new ArgumentException(
                "A default language is required.",
                nameof(defaultLanguage));
        }

        var normalizedTranslations =
            new Dictionary<LanguageCode, string>();

        foreach (var translation in translations)
        {
            if (translation.Key == default)
            {
                throw new ArgumentException(
                    "Every translation must have a language code.",
                    nameof(translations));
            }

            if (string.IsNullOrWhiteSpace(translation.Value))
            {
                throw new ArgumentException(
                    $"Translation for '{translation.Key}' cannot be empty.",
                    nameof(translations));
            }

            var text = translation.Value.Trim();

            if (!normalizedTranslations.TryAdd(translation.Key, text))
            {
                throw new ArgumentException(
                    $"A translation for '{translation.Key}' already exists.",
                    nameof(translations));
            }
        }

        if (normalizedTranslations.Count == 0)
        {
            throw new ArgumentException(
                "At least one translation is required.",
                nameof(translations));
        }

        if (!normalizedTranslations.ContainsKey(defaultLanguage))
        {
            throw new ArgumentException(
                "The default-language translation is required.",
                nameof(translations));
        }

        return new LocalizedText(defaultLanguage, normalizedTranslations);
    }

    public string Get(LanguageCode language)
    {
        if (_translations.TryGetValue(language, out var text))
            return text;

        throw new KeyNotFoundException(
            $"No translation exists for '{language}'.");
    }

    public string GetOrDefault(LanguageCode requestedLanguage)
    {
        return _translations.TryGetValue(requestedLanguage, out var text)
            ? text
            : _translations[DefaultLanguage];
    }

    public bool Equals(LocalizedText? other)
    {
        if (other is null ||
            DefaultLanguage != other.DefaultLanguage ||
            _translations.Count != other._translations.Count)
        {
            return false;
        }

        return _translations.All(pair =>
            other._translations.TryGetValue(pair.Key, out var otherText) &&
            StringComparer.Ordinal.Equals(pair.Value, otherText));
    }

    public override bool Equals(object? obj)
        => obj is LocalizedText other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(DefaultLanguage);

        foreach (var translation in _translations
                     .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
        {
            hash.Add(translation.Key);
            hash.Add(translation.Value, StringComparer.Ordinal);
        }

        return hash.ToHashCode();
    }

    public override string ToString()
        => GetOrDefault(DefaultLanguage);
}