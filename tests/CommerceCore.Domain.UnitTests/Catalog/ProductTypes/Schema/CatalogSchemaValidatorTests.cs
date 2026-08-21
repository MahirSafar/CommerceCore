using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes.Schema;

public sealed class CatalogSchemaValidatorTests
{
    private readonly CatalogSchemaValidator _validator = new();

    [Fact]
    public void ValidateProductSpecifications_WithValidValues_ReturnsValid()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "ram_gb",
                dataType: AttributeDataType.Integer,
                minimumValue: 4,
                maximumValue: 256),
            CreateDefinition(
                key: "screen_size",
                dataType: AttributeDataType.Decimal,
                minimumValue: 10,
                maximumValue: 20),
            CreateDefinition(
                key: "material",
                dataType: AttributeDataType.Text,
                minimumLength: 3,
                maximumLength: 50));

        AttributeValueBag specifications = AttributeValueBag.Empty
            .With(
                AttributeKey.Create("ram_gb"),
                AttributeValue.Integer.Create(16))
            .With(
                AttributeKey.Create("screen_size"),
                AttributeValue.Decimal.Create(15.6m))
            .With(
                AttributeKey.Create("material"),
                AttributeValue.Text.Create("Aluminum"));

        CatalogSchemaValidationResult result =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                specifications,
                schema);

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ValidateProductSpecifications_WithUnknownOrVariantAttribute_ReturnsErrors()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "ram_gb",
                dataType: AttributeDataType.Integer),
            CreateDefinition(
                key: "color",
                dataType: AttributeDataType.SingleSelect,
                scope: AttributeScope.VariantOption));

        AttributeValueBag specifications = AttributeValueBag.Empty
            .With(
                AttributeKey.Create("unknown_key"),
                AttributeValue.Text.Create("value"))
            .With(
                AttributeKey.Create("color"),
                AttributeValue.SingleSelect.Create("space-black"));

        CatalogSchemaValidationResult result =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                specifications,
                schema);

        Assert.False(result.IsValid);

        Assert.Contains(
            result.Errors,
            error => error.Code == "catalog_schema.unknown_attribute");

        Assert.Contains(
            result.Errors,
            error => error.Code == "catalog_schema.invalid_scope");
    }

    [Fact]
    public void ValidateProductSpecifications_WithWrongValueType_ReturnsError()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "ram_gb",
                dataType: AttributeDataType.Integer));

        AttributeValueBag specifications = AttributeValueBag.Empty
            .With(
                AttributeKey.Create("ram_gb"),
                AttributeValue.Text.Create("16 GB"));

        CatalogSchemaValidationResult result =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                specifications,
                schema);

        Assert.Contains(
            result.Errors,
            error => error.Code == "catalog_schema.attribute_type_mismatch");
    }

    [Fact]
    public void ValidateProductSpecifications_RequiresOnlyEnforcedRequiredAttributes()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "ram_gb",
                dataType: AttributeDataType.Integer,
                isRequired: true,
                enforcementStatus: AttributeEnforcementStatus.Enforced),
            CreateDefinition(
                key: "future_field",
                dataType: AttributeDataType.Text,
                isRequired: true,
                enforcementStatus: AttributeEnforcementStatus.Draft),
            CreateDefinition(
                key: "backfill_field",
                dataType: AttributeDataType.Text,
                isRequired: true,
                enforcementStatus: AttributeEnforcementStatus.Backfilling));

        CatalogSchemaValidationResult result =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                AttributeValueBag.Empty,
                schema);

        CatalogSchemaValidationError error = Assert.Single(result.Errors);

        Assert.Equal("ram_gb", error.AttributeKey.Value);
        Assert.Equal(
            "catalog_schema.required_attribute_missing",
            error.Code);
    }

    [Fact]
    public void ValidateProductSpecifications_ValidatesTextAndNumericLimits()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "title",
                dataType: AttributeDataType.Text,
                minimumLength: 3,
                maximumLength: 5),
            CreateDefinition(
                key: "ram_gb",
                dataType: AttributeDataType.Integer,
                minimumValue: 4,
                maximumValue: 128),
            CreateDefinition(
                key: "weight",
                dataType: AttributeDataType.Measurement,
                minimumValue: 100,
                maximumValue: 1000,
                measurementUnitFamily:
                    MeasurementUnitFamily.Create("mass")));

        AttributeValueBag specifications = AttributeValueBag.Empty
            .With(
                AttributeKey.Create("title"),
                AttributeValue.Text.Create("ab"))
            .With(
                AttributeKey.Create("ram_gb"),
                AttributeValue.Integer.Create(256))
            .With(
                AttributeKey.Create("weight"),
                AttributeValue.Measurement.Create(
                    value: 0.01m,
                    unit: "kg",
                    canonicalValue: 10m,
                    canonicalUnit: "g"));

        CatalogSchemaValidationResult result =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                specifications,
                schema);

        Assert.Contains(
            result.Errors,
            error => error.Code == "catalog_schema.minimum_length");

        Assert.Contains(
            result.Errors,
            error => error.Code == "catalog_schema.maximum_value");

        Assert.Contains(
            result.Errors,
            error => error.AttributeKey.Value == "weight" &&
                     error.Code == "catalog_schema.minimum_value");
    }

    [Fact]
    public void ValidateProductSpecifications_WithUnknownOrDeprecatedOption_ReturnsError()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "finish",
                dataType: AttributeDataType.SingleSelect,
                options:
                [
                    new EffectiveAttributeOption(
                        AttributeOptionCode.Create("space-black"),
                        IsDeprecated: false),
                    new EffectiveAttributeOption(
                        AttributeOptionCode.Create("old-gold"),
                        IsDeprecated: true)
                ]));

        CatalogSchemaValidationResult unknownOptionResult =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("finish"),
                    AttributeValue.SingleSelect.Create("unknown")),
                schema);

        CatalogSchemaValidationResult deprecatedOptionResult =
            _validator.ValidateProductSpecifications(
                AttributeValueBag.Empty,
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("finish"),
                    AttributeValue.SingleSelect.Create("old-gold")),
                schema);

        Assert.Contains(
            unknownOptionResult.Errors,
            error => error.Code == "catalog_schema.option_not_allowed");

        Assert.Contains(
            deprecatedOptionResult.Errors,
            error => error.Code ==
                "catalog_schema.deprecated_option_read_only");
    }

    [Fact]
    public void ValidateProductSpecifications_AllowsUnchangedDeprecatedValues_ButBlocksChanges()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            CreateDefinition(
                key: "legacy_code",
                dataType: AttributeDataType.Text,
                isDeprecated: true));

        AttributeKey key = AttributeKey.Create("legacy_code");

        AttributeValueBag current = AttributeValueBag.Empty.With(
            key,
            AttributeValue.Text.Create("old-value"));

        CatalogSchemaValidationResult unchangedResult =
            _validator.ValidateProductSpecifications(
                current,
                current,
                schema);

        CatalogSchemaValidationResult changedResult =
            _validator.ValidateProductSpecifications(
                current,
                current.With(
                    key,
                    AttributeValue.Text.Create("new-value")),
                schema);

        Assert.True(unchangedResult.IsValid);

        Assert.Contains(
            changedResult.Errors,
            error => error.Code ==
                "catalog_schema.deprecated_attribute_read_only");
    }

    private static EffectiveProductTypeSchema CreateSchema(
        params EffectiveAttributeDefinition[] attributes)
    {
        return new EffectiveProductTypeSchema(
            EffectiveSchemaVersion: 1,
            Attributes: attributes);
    }

    private static EffectiveAttributeDefinition CreateDefinition(
        string key,
        AttributeDataType dataType,
        AttributeScope scope = AttributeScope.ProductSpecification,
        bool isRequired = false,
        AttributeEnforcementStatus enforcementStatus =
            AttributeEnforcementStatus.Enforced,
        bool isDeprecated = false,
        int? minimumLength = null,
        int? maximumLength = null,
        decimal? minimumValue = null,
        decimal? maximumValue = null,
        MeasurementUnitFamily? measurementUnitFamily = null,
        IReadOnlyList<EffectiveAttributeOption>? options = null)
    {
        return new EffectiveAttributeDefinition(
            AttributeKey.Create(key),
            dataType,
            scope,
            isRequired,
            enforcementStatus,
            isDeprecated,
            minimumLength,
            maximumLength,
            minimumValue,
            maximumValue,
            measurementUnitFamily,
            options ?? []);
    }
}