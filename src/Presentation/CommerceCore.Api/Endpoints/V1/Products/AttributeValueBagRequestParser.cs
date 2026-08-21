using System.Text.Json;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;
using FluentValidation.Results;

namespace CommerceCore.Api.Endpoints.V1.Products;

internal static class AttributeValueBagRequestParser
{
    public static AttributeValueBag Parse(JsonElement specifications)
    {
        List<ValidationFailure> failures = [];

        if (specifications.ValueKind != JsonValueKind.Object)
        {
            throw CreateException(
                "specifications",
                "specifications.must_be_object",
                "Specifications must be a JSON object.");
        }

        JsonProperty[] properties = [.. specifications.EnumerateObject()];

        if (properties.Length > 50)
        {
            failures.Add(CreateFailure(
                "specifications",
                "specifications.too_many_attributes",
                "A product can contain at most 50 specification attributes."));
        }

        AttributeValueBag result = AttributeValueBag.Empty;
        HashSet<AttributeKey> keys = [];

        foreach (JsonProperty property in properties)
        {
            string propertyPath = $"specifications.{property.Name}";

            if (!TryCreateKey(
                    property.Name,
                    propertyPath,
                    failures,
                    out AttributeKey key))
            {
                continue;
            }

            if (!keys.Add(key))
            {
                failures.Add(CreateFailure(
                    propertyPath,
                    "specifications.duplicate_key",
                    $"Specification key '{property.Name}' occurs more than once."));

                continue;
            }

            if (!TryParseValue(
                    property.Value,
                    propertyPath,
                    failures,
                    out AttributeValue? value))
            {
                continue;
            }

            result = result.With(key, value!);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return result;
    }

    private static bool TryCreateKey(
        string rawKey,
        string propertyPath,
        List<ValidationFailure> failures,
        out AttributeKey key)
    {
        try
        {
            key = AttributeKey.Create(rawKey);
            return true;
        }
        catch (ArgumentException)
        {
            key = default;

            failures.Add(CreateFailure(
                propertyPath,
                "specifications.invalid_key",
                $"'{rawKey}' is not a valid specification key."));

            return false;
        }
    }

    private static bool TryParseValue(
        JsonElement element,
        string propertyPath,
        List<ValidationFailure> failures,
        out AttributeValue? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "specifications.value_must_be_tagged_object",
                "Each specification value must be an object containing 't' and 'v'."));

            return false;
        }

        if (!TryGetString(
                element,
                "t",
                propertyPath,
                failures,
                out string? tag))
        {
            return false;
        }

        if (!element.TryGetProperty("v", out JsonElement rawValue))
        {
            failures.Add(CreateFailure(
                propertyPath,
                "specifications.value_missing",
                "Each specification value must contain 'v'."));

            return false;
        }

        try
        {
            value = tag switch
            {
                "text" => ParseText(rawValue),
                "integer" => ParseInteger(rawValue),
                "decimal" => ParseDecimal(rawValue),
                "boolean" => ParseBoolean(rawValue),
                "singleSelect" => ParseSingleSelect(rawValue),
                "multiSelect" => ParseMultiSelect(rawValue),
                "measurement" => throw new NotSupportedException(),
                _ => throw new ArgumentException()
            };

            return true;
        }
        catch (NotSupportedException)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "specifications.measurement_requires_normalization",
                "Measurement values require server-side unit normalization and are not available yet."));

            return false;
        }
        catch (ArgumentException)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "specifications.invalid_value",
                $"The value is invalid for tag '{tag}'."));

            return false;
        }
    }

    private static AttributeValue.Text ParseText(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new ArgumentException();

        return AttributeValue.Text.Create(value.GetString()!);
    }

    private static AttributeValue.Integer ParseInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long integer))
        {
            throw new ArgumentException();
        }

        return AttributeValue.Integer.Create(integer);
    }

    private static AttributeValue.Decimal ParseDecimal(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDecimal(out decimal decimalValue))
        {
            throw new ArgumentException();
        }

        return AttributeValue.Decimal.Create(decimalValue);
    }

    private static AttributeValue.Boolean ParseBoolean(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.True
            and not JsonValueKind.False)
        {
            throw new ArgumentException();
        }

        return AttributeValue.Boolean.Create(value.GetBoolean());
    }

    private static AttributeValue.SingleSelect ParseSingleSelect(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw new ArgumentException();

        return AttributeValue.SingleSelect.Create(value.GetString()!);
    }

    private static AttributeValue.MultiSelect ParseMultiSelect(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw new ArgumentException();

        List<string> optionCodes = [];

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw new ArgumentException();

            optionCodes.Add(item.GetString()!);
        }

        return AttributeValue.MultiSelect.Create(optionCodes);
    }

    private static bool TryGetString(
        JsonElement element,
        string propertyName,
        string propertyPath,
        List<ValidationFailure> failures,
        out string? value)
    {
        value = null;

        if (!element.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(property.GetString()))
        {
            failures.Add(CreateFailure(
                propertyPath,
                "specifications.tag_missing",
                "Each specification value must contain a non-empty string 't' tag."));

            return false;
        }

        value = property.GetString();
        return true;
    }

    private static ValidationException CreateException(
        string propertyName,
        string errorCode,
        string message) => new ValidationException(
        [
            CreateFailure(propertyName, errorCode, message)
        ]);

    private static ValidationFailure CreateFailure(
        string propertyName,
        string errorCode,
        string message) => new ValidationFailure(propertyName, message)
        {
            ErrorCode = errorCode
        };
}