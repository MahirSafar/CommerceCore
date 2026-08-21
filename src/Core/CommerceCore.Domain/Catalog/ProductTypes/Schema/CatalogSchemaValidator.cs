using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.Catalog.ProductTypes.Schema;

public sealed class CatalogSchemaValidator : ICatalogSchemaValidator
{
    public CatalogSchemaValidationResult ValidateProductSpecifications(
        AttributeValueBag currentSpecifications,
        AttributeValueBag proposedSpecifications,
        EffectiveProductTypeSchema schema)
    {
        ArgumentNullException.ThrowIfNull(currentSpecifications);
        ArgumentNullException.ThrowIfNull(proposedSpecifications);
        ArgumentNullException.ThrowIfNull(schema);

        if (schema.EffectiveSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(schema),
                "Effective schema version must be positive.");
        }

        Dictionary<AttributeKey, EffectiveAttributeDefinition> definitions =
            CreateDefinitionMap(schema);

        List<CatalogSchemaValidationError> errors = [];

        foreach ((AttributeKey key, AttributeValue value)
                 in proposedSpecifications.Values)
        {
            if (!definitions.TryGetValue(key, out EffectiveAttributeDefinition? definition))
            {
                AddError(
                    errors,
                    key,
                    "catalog_schema.unknown_attribute",
                    $"Attribute '{key.Value}' is not defined for this product type.");

                continue;
            }

            if (definition.Scope != AttributeScope.ProductSpecification)
            {
                AddError(
                    errors,
                    key,
                    "catalog_schema.invalid_scope",
                    $"Attribute '{key.Value}' belongs to variant options, not product specifications.");

                continue;
            }

            bool isUnchanged = IsUnchanged(
                currentSpecifications,
                key,
                value);

            if (definition.IsDeprecated)
            {
                if (!isUnchanged)
                {
                    AddError(
                        errors,
                        key,
                        "catalog_schema.deprecated_attribute_read_only",
                        $"Deprecated attribute '{key.Value}' cannot be added or changed.");
                }

                continue;
            }

            ValidateValue(
                errors,
                key,
                value,
                definition,
                isUnchanged);
        }

        foreach (EffectiveAttributeDefinition definition in definitions.Values)
        {
            if (definition.Scope != AttributeScope.ProductSpecification ||
                definition.IsDeprecated ||
                !definition.IsRequired ||
                definition.EnforcementStatus != AttributeEnforcementStatus.Enforced)
            {
                continue;
            }

            if (!proposedSpecifications.Contains(definition.Key))
            {
                AddError(
                    errors,
                    definition.Key,
                    "catalog_schema.required_attribute_missing",
                    $"Required attribute '{definition.Key.Value}' is missing.");
            }
        }

        return new CatalogSchemaValidationResult(errors);
    }

    private static Dictionary<AttributeKey, EffectiveAttributeDefinition>
        CreateDefinitionMap(EffectiveProductTypeSchema schema)
    {
        Dictionary<AttributeKey, EffectiveAttributeDefinition> definitions = [];

        foreach (EffectiveAttributeDefinition definition in schema.Attributes)
        {
            if (!definitions.TryAdd(definition.Key, definition))
            {
                throw new InvalidOperationException(
                    $"Effective schema contains duplicate key '{definition.Key.Value}'.");
            }
        }

        return definitions;
    }

    private static void ValidateValue(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        AttributeValue value,
        EffectiveAttributeDefinition definition,
        bool isUnchanged)
    {
        switch (definition.DataType)
        {
            case AttributeDataType.Text when value is AttributeValue.Text text:
                ValidateText(errors, key, text, definition);
                return;

            case AttributeDataType.Integer when value is AttributeValue.Integer integer:
                ValidateNumber(
                    errors,
                    key,
                    integer.Value,
                    definition);
                return;

            case AttributeDataType.Decimal when value is AttributeValue.Decimal decimalValue:
                ValidateNumber(
                    errors,
                    key,
                    decimalValue.Value,
                    definition);
                return;

            case AttributeDataType.Boolean when value is AttributeValue.Boolean:
                return;

            case AttributeDataType.SingleSelect
                when value is AttributeValue.SingleSelect singleSelect:

                ValidateOption(
                    errors,
                    key,
                    singleSelect.OptionCode,
                    definition,
                    isUnchanged);

                return;

            case AttributeDataType.MultiSelect
                when value is AttributeValue.MultiSelect multiSelect:

                foreach (string optionCode in multiSelect.OptionCodes)
                {
                    ValidateOption(
                        errors,
                        key,
                        optionCode,
                        definition,
                        isUnchanged);
                }

                return;

            case AttributeDataType.Measurement
                when value is AttributeValue.Measurement measurement:

                ValidateMeasurement(
                    errors,
                    key,
                    measurement,
                    definition);

                return;

            default:
                AddError(
                    errors,
                    key,
                    "catalog_schema.attribute_type_mismatch",
                    $"Attribute '{key.Value}' must have type '{definition.DataType}'.");
                return;
        }
    }

    private static void ValidateText(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        AttributeValue.Text value,
        EffectiveAttributeDefinition definition)
    {
        if (definition.MinimumLength is int minimumLength &&
            value.Value.Length < minimumLength)
        {
            AddError(
                errors,
                key,
                "catalog_schema.minimum_length",
                $"Attribute '{key.Value}' must contain at least {minimumLength} characters.");
        }

        if (definition.MaximumLength is int maximumLength &&
            value.Value.Length > maximumLength)
        {
            AddError(
                errors,
                key,
                "catalog_schema.maximum_length",
                $"Attribute '{key.Value}' must contain at most {maximumLength} characters.");
        }
    }

    private static void ValidateNumber(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        decimal value,
        EffectiveAttributeDefinition definition)
    {
        if (definition.MinimumValue is decimal minimumValue &&
            value < minimumValue)
        {
            AddError(
                errors,
                key,
                "catalog_schema.minimum_value",
                $"Attribute '{key.Value}' must be at least {minimumValue}.");
        }

        if (definition.MaximumValue is decimal maximumValue &&
            value > maximumValue)
        {
            AddError(
                errors,
                key,
                "catalog_schema.maximum_value",
                $"Attribute '{key.Value}' must be at most {maximumValue}.");
        }
    }

    private static void ValidateMeasurement(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        AttributeValue.Measurement measurement,
        EffectiveAttributeDefinition definition)
    {
        if (definition.MeasurementUnitFamily is null)
        {
            AddError(
                errors,
                key,
                "catalog_schema.invalid_measurement_definition",
                $"Measurement attribute '{key.Value}' has no unit family.");

            return;
        }

        ValidateNumber(
            errors,
            key,
            measurement.CanonicalValue,
            definition);
    }

    private static void ValidateOption(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        string optionCode,
        EffectiveAttributeDefinition definition,
        bool isUnchanged)
    {
        EffectiveAttributeOption? option = definition.Options
            .SingleOrDefault(item =>
                string.Equals(
                    item.Code.Value,
                    optionCode,
                    StringComparison.Ordinal));

        if (option is null)
        {
            AddError(
                errors,
                key,
                "catalog_schema.option_not_allowed",
                $"Option '{optionCode}' is not allowed for attribute '{key.Value}'.");

            return;
        }

        if (option.IsDeprecated && !isUnchanged)
        {
            AddError(
                errors,
                key,
                "catalog_schema.deprecated_option_read_only",
                $"Deprecated option '{optionCode}' cannot be added or changed.");
        }
    }

    private static bool IsUnchanged(
        AttributeValueBag currentSpecifications,
        AttributeKey key,
        AttributeValue proposedValue)
    {
        return currentSpecifications.TryGetValue(
                   key,
                   out AttributeValue? currentValue) &&
               Equals(currentValue, proposedValue);
    }

    private static void AddError(
        List<CatalogSchemaValidationError> errors,
        AttributeKey key,
        string code,
        string message)
    {
        errors.Add(new CatalogSchemaValidationError(
            key,
            code,
            message));
    }
}