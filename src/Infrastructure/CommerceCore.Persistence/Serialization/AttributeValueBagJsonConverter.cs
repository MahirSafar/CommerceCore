using System.Text.Json;
using System.Text.Json.Serialization;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Persistence.Serialization;

public sealed class AttributeValueBagJsonConverter
    : JsonConverter<AttributeValueBag>
{
    private const string TagText = "text";
    private const string TagInteger = "integer";
    private const string TagDecimal = "decimal";
    private const string TagBoolean = "boolean";
    private const string TagSingleSelect = "singleSelect";
    private const string TagMultiSelect = "multiSelect";
    private const string TagMeasurement = "measurement";

    public override AttributeValueBag Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException(
                "An attribute-value bag must be a JSON object.");
        }

        using JsonDocument document = JsonDocument.ParseValue(ref reader);

        AttributeValueBag bag = AttributeValueBag.Empty;

        foreach (JsonProperty property in document.RootElement.EnumerateObject())
        {
            bag = bag.With(
                AttributeKey.Create(property.Name),
                ReadTaggedValue(property.Value));
        }

        return bag;
    }

    public override void Write(
        Utf8JsonWriter writer,
        AttributeValueBag value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        foreach ((AttributeKey key, AttributeValue attributeValue) in value.Values
            .OrderBy(item => item.Key.Value, StringComparer.Ordinal))
        {
            writer.WritePropertyName(key.Value);
            WriteTaggedValue(writer, attributeValue);
        }

        writer.WriteEndObject();
    }

    private static AttributeValue ReadTaggedValue(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new JsonException(
                "An attribute value must be a tagged JSON object.");
        }

        string tag = GetRequiredStringProperty(element, "t");
        JsonElement value = GetRequiredProperty(element, "v");

        return tag switch
        {
            TagText => AttributeValue.Text.Create(
                GetRequiredString(value)),

            TagInteger => AttributeValue.Integer.Create(
                value.GetInt64()),

            TagDecimal => AttributeValue.Decimal.Create(
                value.GetDecimal()),

            TagBoolean => AttributeValue.Boolean.Create(
                value.GetBoolean()),

            TagSingleSelect => AttributeValue.SingleSelect.Create(
                GetRequiredString(value)),

            TagMultiSelect => AttributeValue.MultiSelect.Create(
                ReadStringArray(value)),

            TagMeasurement => AttributeValue.Measurement.Create(
                GetRequiredProperty(value, "value").GetDecimal(),
                GetRequiredStringProperty(value, "unit"),
                GetRequiredProperty(value, "canonicalValue").GetDecimal(),
                GetRequiredStringProperty(value, "canonicalUnit")),

            _ => throw new JsonException(
                $"Unknown attribute-value tag '{tag}'.")
        };
    }

    private static void WriteTaggedValue(
        Utf8JsonWriter writer,
        AttributeValue value)
    {
        ArgumentNullException.ThrowIfNull(value);

        writer.WriteStartObject();

        switch (value)
        {
            case AttributeValue.Text text:
                writer.WriteString("t", TagText);
                writer.WriteString("v", text.Value);
                break;

            case AttributeValue.Integer integer:
                writer.WriteString("t", TagInteger);
                writer.WriteNumber("v", integer.Value);
                break;

            case AttributeValue.Decimal decimalValue:
                writer.WriteString("t", TagDecimal);
                writer.WriteNumber("v", decimalValue.Value);
                break;

            case AttributeValue.Boolean boolean:
                writer.WriteString("t", TagBoolean);
                writer.WriteBoolean("v", boolean.Value);
                break;

            case AttributeValue.SingleSelect singleSelect:
                writer.WriteString("t", TagSingleSelect);
                writer.WriteString("v", singleSelect.OptionCode);
                break;

            case AttributeValue.MultiSelect multiSelect:
                writer.WriteString("t", TagMultiSelect);
                writer.WritePropertyName("v");
                writer.WriteStartArray();

                foreach (string optionCode in multiSelect.OptionCodes)
                {
                    writer.WriteStringValue(optionCode);
                }

                writer.WriteEndArray();
                break;

            case AttributeValue.Measurement measurement:
                writer.WriteString("t", TagMeasurement);
                writer.WritePropertyName("v");
                writer.WriteStartObject();
                writer.WriteNumber("value", measurement.Value);
                writer.WriteString("unit", measurement.Unit);
                writer.WriteNumber("canonicalValue", measurement.CanonicalValue);
                writer.WriteString("canonicalUnit", measurement.CanonicalUnit);
                writer.WriteEndObject();
                break;

            default:
                throw new JsonException(
                    $"Unsupported attribute-value type '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }

    private static JsonElement GetRequiredProperty(
        JsonElement element,
        string propertyName) =>
        element.TryGetProperty(propertyName, out JsonElement property)
            ? property
            : throw new JsonException(
                $"Attribute value JSON is missing required property '{propertyName}'.");

    private static string GetRequiredStringProperty(
        JsonElement element,
        string propertyName) =>
        GetRequiredString(GetRequiredProperty(element, propertyName));

    private static string GetRequiredString(JsonElement element) =>
        element.ValueKind == JsonValueKind.String &&
        !string.IsNullOrWhiteSpace(element.GetString())
            ? element.GetString()!
            : throw new JsonException(
                "Attribute value JSON must contain a non-empty string.");

    private static IEnumerable<string> ReadStringArray(JsonElement element) =>
        element.ValueKind != JsonValueKind.Array
            ? throw new JsonException(
                "A multi-select attribute value must be a JSON array.")
            : (IEnumerable<string>)[.. element.EnumerateArray().Select(GetRequiredString)];
}