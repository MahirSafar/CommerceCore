using System.Text.Json;
using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence.ProductTypes;

public sealed class ProductTypeEffectiveSchemaReader(
    CommerceCoreDbContext dbContext)
    : IProductTypeEffectiveSchemaReader
{
    private readonly CommerceCoreDbContext _dbContext = dbContext;

    public async Task<EffectiveProductTypeSchema?> GetAsync(
        ProductTypeId productTypeId,
        CancellationToken cancellationToken)
    {
        ProductTypeEffectiveSchema? persistedSchema = await _dbContext
            .Set<ProductTypeEffectiveSchema>()
            .AsNoTracking()
            .SingleOrDefaultAsync(
                schema => schema.ProductTypeId == productTypeId,
                cancellationToken);

        if (persistedSchema is null)
            return null;

        return Deserialize(
            persistedSchema.Schema,
            persistedSchema.EffectiveSchemaVersion);
    }

    private static EffectiveProductTypeSchema Deserialize(
        string schemaJson,
        long effectiveSchemaVersion)
    {
        using JsonDocument document = JsonDocument.Parse(schemaJson);

        JsonElement attributesElement = GetRequiredProperty(
            document.RootElement,
            "attributes");

        if (attributesElement.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException(
                "Effective product-type schema attributes must be an array.");
        }

        EffectiveAttributeDefinition[] attributes = [.. attributesElement
            .EnumerateArray()
            .Select(DeserializeAttribute)];

        return new EffectiveProductTypeSchema(
            effectiveSchemaVersion,
            attributes);
    }

    private static EffectiveAttributeDefinition DeserializeAttribute(
        JsonElement element)
    {
        MeasurementUnitFamily? measurementUnitFamily =
            GetNullableString(element, "measurementUnitFamily") is
            { } unitFamily
                ? MeasurementUnitFamily.Create(unitFamily)
                : null;

        EffectiveAttributeOption[] options = [.. GetRequiredProperty(
                element,
                "options")
            .EnumerateArray()
            .Select(option => new EffectiveAttributeOption(
                AttributeOptionCode.Create(
                    GetRequiredString(option, "code")),
                GetRequiredBoolean(option, "isDeprecated")))];

        return new EffectiveAttributeDefinition(
            AttributeKey.Create(GetRequiredString(element, "key")),
            ParseEnum<AttributeDataType>(element, "dataType"),
            ParseEnum<AttributeScope>(element, "scope"),
            GetRequiredBoolean(element, "isRequired"),
            ParseEnum<AttributeEnforcementStatus>(
                element,
                "enforcementStatus"),
            GetRequiredBoolean(element, "isDeprecated"),
            GetNullableInt32(element, "minimumLength"),
            GetNullableInt32(element, "maximumLength"),
            GetNullableDecimal(element, "minimumValue"),
            GetNullableDecimal(element, "maximumValue"),
            measurementUnitFamily,
            options);
    }

    private static TEnum ParseEnum<TEnum>(
        JsonElement element,
        string propertyName)
        where TEnum : struct, Enum
    {
        string value = GetRequiredString(element, propertyName);

        if (!Enum.TryParse(value, ignoreCase: false, out TEnum result))
        {
            throw new InvalidOperationException(
                $"'{value}' is not a valid {typeof(TEnum).Name}.");
        }

        return result;
    }

    private static JsonElement GetRequiredProperty(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidOperationException(
                $"Effective schema is missing '{propertyName}'.");
        }

        return value;
    }

    private static string GetRequiredString(
        JsonElement element,
        string propertyName)
    {
        JsonElement value = GetRequiredProperty(element, propertyName);

        if (value.ValueKind != JsonValueKind.String ||
            string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidOperationException(
                $"Effective schema property '{propertyName}' must be a string.");
        }

        return value.GetString()!;
    }

    private static bool GetRequiredBoolean(
        JsonElement element,
        string propertyName)
    {
        JsonElement value = GetRequiredProperty(element, propertyName);

        if (value.ValueKind is not JsonValueKind.True
            and not JsonValueKind.False)
        {
            throw new InvalidOperationException(
                $"Effective schema property '{propertyName}' must be a boolean.");
        }

        return value.GetBoolean();
    }

    private static int? GetNullableInt32(
        JsonElement element,
        string propertyName) =>
        GetRequiredProperty(element, propertyName).ValueKind == JsonValueKind.Null
            ? null
            : GetRequiredProperty(element, propertyName).GetInt32();

    private static decimal? GetNullableDecimal(
        JsonElement element,
        string propertyName) =>
        GetRequiredProperty(element, propertyName).ValueKind == JsonValueKind.Null
            ? null
            : GetRequiredProperty(element, propertyName).GetDecimal();

    private static string? GetNullableString(
        JsonElement element,
        string propertyName) =>

        GetRequiredProperty(element, propertyName).ValueKind == JsonValueKind.Null
            ? null
            : GetRequiredProperty(element, propertyName).GetString();
}