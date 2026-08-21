using System.Buffers;
using System.Text.Json;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;

namespace CommerceCore.Api.Endpoints.V1.Products;

internal static class AttributeValueBagResponseSerializer
{
    public static JsonElement Serialize(AttributeValueBag specifications)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        ArrayBufferWriter<byte> buffer = new();

        using (Utf8JsonWriter writer = new(buffer))
        {
            writer.WriteStartObject();

            foreach ((var key, var value) in specifications.Values
                         .OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
            {
                writer.WritePropertyName(key.Value);
                WriteValue(writer, value);
            }

            writer.WriteEndObject();
        }

        using JsonDocument document = JsonDocument.Parse(
            buffer.WrittenMemory);

        return document.RootElement.Clone();
    }

    private static void WriteValue(
        Utf8JsonWriter writer,
        AttributeValue value)
    {
        writer.WriteStartObject();

        switch (value)
        {
            case AttributeValue.Text text:
                writer.WriteString("t", "text");
                writer.WriteString("v", text.Value);
                break;

            case AttributeValue.Integer integer:
                writer.WriteString("t", "integer");
                writer.WriteNumber("v", integer.Value);
                break;

            case AttributeValue.Decimal decimalValue:
                writer.WriteString("t", "decimal");
                writer.WriteNumber("v", decimalValue.Value);
                break;

            case AttributeValue.Boolean boolean:
                writer.WriteString("t", "boolean");
                writer.WriteBoolean("v", boolean.Value);
                break;

            case AttributeValue.SingleSelect singleSelect:
                writer.WriteString("t", "singleSelect");
                writer.WriteString("v", singleSelect.OptionCode);
                break;

            case AttributeValue.MultiSelect multiSelect:
                writer.WriteString("t", "multiSelect");
                writer.WritePropertyName("v");
                writer.WriteStartArray();

                foreach (string optionCode in multiSelect.OptionCodes)
                    writer.WriteStringValue(optionCode);

                writer.WriteEndArray();
                break;

            case AttributeValue.Measurement measurement:
                writer.WriteString("t", "measurement");
                writer.WritePropertyName("v");
                writer.WriteStartObject();
                writer.WriteNumber("value", measurement.Value);
                writer.WriteString("unit", measurement.Unit);
                writer.WriteNumber(
                    "canonicalValue",
                    measurement.CanonicalValue);
                writer.WriteString(
                    "canonicalUnit",
                    measurement.CanonicalUnit);
                writer.WriteEndObject();
                break;

            default:
                throw new NotSupportedException(
                    $"Unsupported attribute value type '{value.GetType().Name}'.");
        }

        writer.WriteEndObject();
    }
}