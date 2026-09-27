using System.Text.Json;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Persistence.Serialization;

public static class LocalizedTextJsonSerializer
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string Serialize(LocalizedText value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var document = new LocalizedTextDocument(
            value.DefaultLanguage.Value,
            value.Translations.ToDictionary(
                pair => pair.Key.Value,
                pair => pair.Value,
                StringComparer.Ordinal));

        return JsonSerializer.Serialize(document, JsonOptions);
    }

    public static LocalizedText Deserialize(string json)
    {
        ArgumentNullException.ThrowIfNull(json);

        LocalizedTextDocument document =
            JsonSerializer.Deserialize<LocalizedTextDocument>(
                json,
                JsonOptions)
            ?? throw new InvalidOperationException(
                "LocalizedText JSON cannot be null.");

        if (document.DefaultLanguage is null ||
            document.Translations is null)
        {
            throw new JsonException(
                "LocalizedText requires DefaultLanguage and Translations.");
        }

        return LocalizedText.Create(
            LanguageCode.Create(document.DefaultLanguage),
            document.Translations.Select(pair =>
                new KeyValuePair<LanguageCode, string>(
                    LanguageCode.Create(pair.Key),
                    pair.Value)));
    }

    private sealed record LocalizedTextDocument(
        string? DefaultLanguage,
        Dictionary<string, string>? Translations);
}
