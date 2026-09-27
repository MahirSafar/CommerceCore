using System.Text.Json;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.Serialization;

namespace CommerceCore.Persistence.IntegrationTests.Serialization;

public sealed class LocalizedTextJsonSerializerTests
{
    [Fact]
    public void RoundTrip_PreservesDefaultLanguageAndTranslations()
    {
        LocalizedText original = LocalizedText.Create(
            LanguageCode.Create("en"),
            [
                new(LanguageCode.Create("en"), "Storefront product"),
                new(LanguageCode.Create("az"), "Vitrin məhsulu")
            ]);

        string json = LocalizedTextJsonSerializer.Serialize(original);
        LocalizedText restored = LocalizedTextJsonSerializer.Deserialize(json);

        Assert.Equal(original, restored);
    }

    [Fact]
    public void Serialize_PreservesExistingPropertyNames()
    {
        LocalizedText name = LocalizedText.Create(
            LanguageCode.Create("en"),
            [new(LanguageCode.Create("en"), "Product")]);

        string json = LocalizedTextJsonSerializer.Serialize(name);

        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal(
            "en",
            document.RootElement.GetProperty("DefaultLanguage").GetString());

        Assert.Equal(
            "Product",
            document.RootElement
                .GetProperty("Translations")
                .GetProperty("en")
                .GetString());

        Assert.False(
            document.RootElement.TryGetProperty("defaultLanguage", out _));
    }

    [Theory]
    [InlineData(
        """{"DefaultLanguage":"en","Translations":{"en":"Product","az":"Məhsul"}}""")]
    [InlineData(
        """{"defaultLanguage":"en","translations":{"en":"Product","az":"Məhsul"}}""")]
    public void Deserialize_AcceptsExistingPropertyCasing(string json)
    {
        LocalizedText name = LocalizedTextJsonSerializer.Deserialize(json);

        Assert.Equal(LanguageCode.Create("en"), name.DefaultLanguage);
        Assert.Equal("Məhsul", name.Get(LanguageCode.Create("az")));
        Assert.Equal("Product", name.GetOrDefault(LanguageCode.Create("de")));
    }

    [Theory]
    [InlineData("""{}""")]
    [InlineData("""{"DefaultLanguage":"en"}""")]
    [InlineData("""{"Translations":{"en":"Product"}}""")]
    [InlineData("""{"DefaultLanguage":"en","Translations":null}""")]
    public void Deserialize_IncompleteDocument_ThrowsJsonException(string json)
    {
        Assert.Throws<JsonException>(() =>
            LocalizedTextJsonSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_MissingDefaultTranslation_PreservesDomainValidation()
    {
        const string json =
            """{"DefaultLanguage":"en","Translations":{"az":"Məhsul"}}""";

        Assert.Throws<ArgumentException>(() =>
            LocalizedTextJsonSerializer.Deserialize(json));
    }

    [Fact]
    public void Deserialize_NullDocument_IsRejected()
    {
        Assert.Throws<InvalidOperationException>(() =>
            LocalizedTextJsonSerializer.Deserialize("null"));
    }
}
