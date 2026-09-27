using System.Text.Json;
using CommerceCore.Api.Endpoints.V1.Products;
using FluentValidation;

namespace CommerceCore.Api.UnitTests;

public sealed class AttributeParserResourceLimitTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_WithExactly50Attributes_AcceptsInput(
        bool parseOptions)
    {
        using JsonDocument document = CreateDocument(
            propertyCount: 50);

        int count = ParseCount(document.RootElement, parseOptions);

        Assert.Equal(50, count);
    }

    [Theory]
    [InlineData(false, 51)]
    [InlineData(false, 1000)]
    [InlineData(true, 51)]
    [InlineData(true, 1000)]
    public void Parse_WithTooManyAttributes_ReturnsOnlyCountError(
        bool parseOptions,
        int propertyCount)
    {
        // Every value is invalid. An oversized object must be rejected
        // before individual value validation produces additional errors.
        using JsonDocument document = CreateDocument(
            propertyCount,
            invalidValues: true);

        ValidationException exception =
            Assert.Throws<ValidationException>(
                () => ParseCount(document.RootElement, parseOptions));

        var failure = Assert.Single(exception.Errors);

        string propertyName = parseOptions
            ? "options"
            : "specifications";

        Assert.Equal(propertyName, failure.PropertyName);
        Assert.Equal(
            $"{propertyName}.too_many_attributes",
            failure.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Parse_With51RepeatedProperties_ReturnsOnlyCountError(
        bool parseOptions)
    {
        // Duplicate property names still consume input and processing work.
        // Count raw properties, not only distinct attribute keys.
        using JsonDocument document = CreateDocument(
            propertyCount: 51,
            duplicateKeys: true);

        ValidationException exception =
            Assert.Throws<ValidationException>(
                () => ParseCount(document.RootElement, parseOptions));

        var failure = Assert.Single(exception.Errors);

        string propertyName = parseOptions
            ? "options"
            : "specifications";

        Assert.Equal(propertyName, failure.PropertyName);
        Assert.Equal(
            $"{propertyName}.too_many_attributes",
            failure.ErrorCode);
    }

    private static int ParseCount(
        JsonElement input,
        bool parseOptions) => parseOptions
        ? VariantOptionsRequestParser.Parse(input).Count
        : AttributeValueBagRequestParser.Parse(input).Count;

    private static JsonDocument CreateDocument(
        int propertyCount,
        bool invalidValues = false,
        bool duplicateKeys = false)
    {
        using var stream = new MemoryStream();

        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();

            for (int index = 0; index < propertyCount; index++)
            {
                writer.WritePropertyName(
                    duplicateKeys ? "color" : $"attribute_{index}");

                if (invalidValues)
                {
                    writer.WriteNullValue();
                    continue;
                }

                writer.WriteStartObject();
                writer.WriteString("t", "singleSelect");
                writer.WriteString("v", "black");
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.Flush();
        }

        return JsonDocument.Parse(stream.ToArray());
    }
}
