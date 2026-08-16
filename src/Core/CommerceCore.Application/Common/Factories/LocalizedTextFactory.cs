using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Application.Common.Factories;

public static class LocalizedTextFactory
{
    public static LocalizedText Create(
        string defaultLanguage,
        IReadOnlyDictionary<string, string> translations)
    {
        ArgumentNullException.ThrowIfNull(translations);

        LanguageCode defaultLanguageCode = LanguageCode.Create(defaultLanguage);

        IEnumerable<KeyValuePair<LanguageCode, string>> localizedTranslations = translations.Select(
            translation => new KeyValuePair<LanguageCode, string>(
                LanguageCode.Create(translation.Key),
                translation.Value));

        return LocalizedText.Create(
            defaultLanguageCode,
            localizedTranslations);
    }
}