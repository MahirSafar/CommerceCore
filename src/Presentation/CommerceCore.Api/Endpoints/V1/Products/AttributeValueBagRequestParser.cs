using CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;
using FluentValidation.Results;
using System.Text.Json;

namespace CommerceCore.Api.Endpoints.V1.Products;

public static class AttributeValueBagRequestParser
{
    public static ProductSpecificationsInput Parse(JsonElement specifications)
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

        Dictionary<AttributeKey, ProductSpecificationInputValue> result = [];
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
                    out ProductSpecificationInputValue? value))
            {
                continue;
            }

            result.Add(key, value!);
        }

        if (failures.Count > 0)
            throw new ValidationException(failures);

        return new ProductSpecificationsInput(result);
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
        out ProductSpecificationInputValue? value)
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
                "text" => new ProductSpecificationInputValue.Typed(
                    ParseText(rawValue)),

                "integer" => new ProductSpecificationInputValue.Typed(
                    ParseInteger(rawValue)),

                "decimal" => new ProductSpecificationInputValue.Typed(
                    ParseDecimal(rawValue)),

                "boolean" => new ProductSpecificationInputValue.Typed(
                    ParseBoolean(rawValue)),

                "singleSelect" => new ProductSpecificationInputValue.Typed(
                    ParseSingleSelect(rawValue)),

                "multiSelect" => new ProductSpecificationInputValue.Typed(
                    ParseMultiSelect(rawValue)),

                "measurement" => ParseMeasurement(rawValue),

                _ => throw InvalidValueFormat()
            };

            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or FormatException)
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
            throw InvalidValueFormat();

        return AttributeValue.Text.Create(value.GetString()!);
    }

    private static AttributeValue.Integer ParseInteger(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long integer))
        {
            throw InvalidValueFormat();
        }

        return AttributeValue.Integer.Create(integer);
    }

    private static AttributeValue.Decimal ParseDecimal(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDecimal(out decimal decimalValue))
        {
            throw InvalidValueFormat();
        }

        return AttributeValue.Decimal.Create(decimalValue);
    }

    private static AttributeValue.Boolean ParseBoolean(JsonElement value)
    {
        if (value.ValueKind is not JsonValueKind.True
            and not JsonValueKind.False)
        {
            throw InvalidValueFormat();
        }

        return AttributeValue.Boolean.Create(value.GetBoolean());
    }

    private static AttributeValue.SingleSelect ParseSingleSelect(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String)
            throw InvalidValueFormat();

        return AttributeValue.SingleSelect.Create(value.GetString()!);
    }

    private static AttributeValue.MultiSelect ParseMultiSelect(
        JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Array)
            throw InvalidValueFormat();

        List<string> optionCodes = [];

        foreach (JsonElement item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                throw InvalidValueFormat();

            optionCodes.Add(item.GetString()!);
        }

        return AttributeValue.MultiSelect.Create(optionCodes);
    }

    private static ProductSpecificationInputValue.Measurement ParseMeasurement(
    JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            throw InvalidValueFormat();

        JsonProperty[] properties = [.. value.EnumerateObject()];

        if (properties.Length != 2 ||
            !value.TryGetProperty("value", out JsonElement rawAmount) ||
            !value.TryGetProperty("unit", out JsonElement rawUnit) ||
            rawAmount.ValueKind != JsonValueKind.Number ||
            !rawAmount.TryGetDecimal(out decimal amount) ||
            rawUnit.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(rawUnit.GetString()))
        {
            throw InvalidValueFormat();
        }

        foreach (JsonProperty property in properties)
        {
            if (property.Name is not "value" and not "unit")
                throw InvalidValueFormat();
        }

        return new ProductSpecificationInputValue.Measurement(
            amount,
            rawUnit.GetString()!);
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

    private static FormatException InvalidValueFormat() =>
        new("The tagged specification value has an invalid JSON format.");

    private static ValidationException CreateException(
        string propertyName,
        string errorCode,
        string message) => new(
        [
            CreateFailure(propertyName, errorCode, message)
        ]);

    private static ValidationFailure CreateFailure(
        string propertyName,
        string errorCode,
        string message) => new(propertyName, message)
        {
            ErrorCode = errorCode
        };
}