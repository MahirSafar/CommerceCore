using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Common.ValueObjects.Localization;

public class LocalizedTextTests
{
    [Fact]
    public void Create_WithoutDefaultLanguageTranslation_ShouldThrowArgumentException()
    {
        var defaultLang = LanguageCode.Create("en");
        var translations = new Dictionary<LanguageCode, string>
        {
            { LanguageCode.Create("az"), "Noutbuk" }
        };

        var exception = Assert.Throws<ArgumentException>(() =>
            LocalizedText.Create(defaultLang, translations));

        Assert.Contains("default-language translation is required", exception.Message);
    }

    [Fact]
    public void Create_WithEmptyTranslation_ShouldThrowArgumentException()
    {
        var lang = LanguageCode.Create("en");
        var translations = new Dictionary<LanguageCode, string>
        {
            { lang, "" }
        };

        var ex = Assert.Throws<ArgumentException>(() => LocalizedText.Create(lang, translations));
        Assert.Contains("cannot be empty", ex.Message);
    }

    [Fact]
    public void Create_WithDuplicateLanguage_ShouldThrowArgumentException()
    {
        var lang = LanguageCode.Create("en");
        var translations = new List<KeyValuePair<LanguageCode, string>>
        {
            new(lang, "A"),
            new(lang, "B"),
        };

        var ex = Assert.Throws<ArgumentException>(() => LocalizedText.Create(lang, translations));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void Create_WithEmptyTranslationsCollection_ShouldThrowArgumentException()
    {
        var lang = LanguageCode.Create("en");
        var translations = new List<KeyValuePair<LanguageCode, string>>();

        var ex = Assert.Throws<ArgumentException>(() => LocalizedText.Create(lang, translations));
        Assert.Contains("At least one translation is required", ex.Message);
    }

    [Fact]
    public void GetOrDefault_ReturnsDefaultLanguage_WhenRequestedMissing()
    {
        var defaultLang = LanguageCode.Create("en");
        var otherLang = LanguageCode.Create("az");
        var translations = new Dictionary<LanguageCode, string>
        {
            { defaultLang, "Notebook" }
        };

        var localized = LocalizedText.Create(defaultLang, translations);

        var result = localized.GetOrDefault(otherLang);

        Assert.Equal("Notebook", result);
    }
}
