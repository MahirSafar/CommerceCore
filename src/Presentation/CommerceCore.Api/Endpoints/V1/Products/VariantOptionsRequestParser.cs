using System.Text.Json;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;
using FluentValidation.Results;

namespace CommerceCore.Api.Endpoints.V1.Products;

public static class VariantOptionsRequestParser
{
    public static AttributeValueBag Parse(JsonElement options)
    {
        if (options.ValueKind != JsonValueKind.Object)
        {
            throw CreateException(
                "options",
                "options.must_be_object",
                "Variant options must be a JSON object.");
        }

        JsonProperty[] properties = [.. options.EnumerateObject()];
        List<ValidationFailure> failures = [];

        if (properties.Length > 50)
        {
            failures.Add(CreateFailure(
                "options",
                "options.too_many_attributes",
                "A variant can contain at most 50 option attributes."));
        }

        AttributeValueBag result = AttributeValueBag.Empty;
        HashSet<AttributeKey> keys = [];

        foreach (JsonProperty property in properties)
        {
            string propertyPath = $"options.{property.Name}";

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
                    "options.duplicate_key",
                    $"Option key '{property.Name}' occurs more than once."));

                continue;
            }

            if (!TryParseSingleSelect(
                    property.Value,
                    propertyPath,
                    failures,
                    out AttributeValue.SingleSelect? value))
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
                "options.invalid_key",
                $"'{rawKey}' is not a valid option key."));

            return false;
        }
    }

    private static bool TryParseSingleSelect(
        JsonElement element,
        string propertyPath,
        List<ValidationFailure> failures,
        out AttributeValue.SingleSelect? value)
    {
        value = null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "options.value_must_be_tagged_object",
                "Each option value must be an object containing 't' and 'v'."));

            return false;
        }

        JsonProperty[] properties = [.. element.EnumerateObject()];

        if (properties.Length != 2)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "options.value_invalid_shape",
                "Each option value must contain exactly 't' and 'v'."));

            return false;
        }

        if (!element.TryGetProperty("t", out JsonElement rawTag) ||
            rawTag.ValueKind != JsonValueKind.String ||
            !string.Equals(
                rawTag.GetString(),
                "singleSelect",
                StringComparison.Ordinal))
        {
            failures.Add(CreateFailure(
                propertyPath,
                "options.variant_option_must_be_single_select",
                "Variant option values must use the 'singleSelect' tag."));

            return false;
        }

        if (!element.TryGetProperty("v", out JsonElement rawValue) ||
            rawValue.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(rawValue.GetString()))
        {
            failures.Add(CreateFailure(
                propertyPath,
                "options.invalid_value",
                "A singleSelect option value must be a non-empty string."));

            return false;
        }

        try
        {
            value = AttributeValue.SingleSelect.Create(
                rawValue.GetString()!);

            return true;
        }
        catch (ArgumentException)
        {
            failures.Add(CreateFailure(
                propertyPath,
                "options.invalid_value",
                "A singleSelect option value is invalid."));

            return false;
        }
    }

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