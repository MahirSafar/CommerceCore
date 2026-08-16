using CommerceCore.Domain.Common.ValueObjects.Localization;
using FluentValidation;

namespace CommerceCore.Application.Common.Validation;

public static class LocalizedTextValidationRules
{
    public const string LanguageTagPattern = "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$";

    public static void Validate<T>(
        string? defaultLanguage,
        IReadOnlyDictionary<string, string>? translations,
        ValidationContext<T> context,
        string translationsPropertyName,
        int maximumTextLength)
    {
        if (translations is null)
            return;

        HashSet<LanguageCode> languages = new HashSet<LanguageCode>();

        foreach (KeyValuePair<string, string> translation in translations)
        {
            LanguageCode language;

            try
            {
                language = LanguageCode.Create(translation.Key);
            }
            catch (ArgumentException)
            {
                context.AddFailure(
                    translationsPropertyName,
                    $"'{translation.Key}' is not a valid language code.");

                continue;
            }

            if (!languages.Add(language))
            {
                context.AddFailure(
                    translationsPropertyName,
                    $"Duplicate translation exists for '{language}'.");
            }

            if (string.IsNullOrWhiteSpace(translation.Value))
            {
                context.AddFailure(
                    translationsPropertyName,
                    $"Translation for '{language}' cannot be empty.");

                continue;
            }

            if (translation.Value.Trim().Length > maximumTextLength)
            {
                context.AddFailure(
                    translationsPropertyName,
                    $"Translation for '{language}' cannot exceed " +
                    $"{maximumTextLength} characters.");
            }
        }

        if (string.IsNullOrWhiteSpace(defaultLanguage))
            return;

        try
        {
            LanguageCode normalizedDefaultLanguage =
                LanguageCode.Create(defaultLanguage);

            if (!languages.Contains(normalizedDefaultLanguage))
            {
                context.AddFailure(
                    translationsPropertyName,
                    "A translation for the default language is required.");
            }
        }
        catch (ArgumentException)
        {
        }
    }
}