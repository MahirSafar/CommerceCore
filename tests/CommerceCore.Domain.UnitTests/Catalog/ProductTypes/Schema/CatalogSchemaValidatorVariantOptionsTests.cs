using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes.Schema;

public sealed class CatalogSchemaValidatorVariantOptionsTests
{
    private readonly CatalogSchemaValidator _validator = new();

    [Fact]
    public void ValidateVariantOptions_WithAllowedOption_IsValid()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            VariantColorDefinition(
                isRequired: true,
                isDeprecated: false,
                optionIsDeprecated: false));

        CatalogSchemaValidationResult result =
            _validator.ValidateVariantOptions(
                AttributeValueBag.Empty,
                Options("space-black"),
                schema);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateVariantOptions_WithProductSpecificationKey_ReturnsInvalidScope()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            new EffectiveAttributeDefinition(
                AttributeKey.Create("ram_gb"),
                AttributeDataType.Integer,
                AttributeScope.ProductSpecification,
                IsRequired: false,
                AttributeEnforcementStatus.Enforced,
                IsDeprecated: false,
                MinimumLength: null,
                MaximumLength: null,
                MinimumValue: 4m,
                MaximumValue: 256m,
                MeasurementUnitFamily: null,
                Options: []));

        CatalogSchemaValidationResult result =
            _validator.ValidateVariantOptions(
                AttributeValueBag.Empty,
                AttributeValueBag.Empty.With(
                    AttributeKey.Create("ram_gb"),
                    AttributeValue.Integer.Create(16)),
                schema);

        CatalogSchemaValidationError error = Assert.Single(result.Errors);

        Assert.Equal("catalog_schema.invalid_scope", error.Code);
    }

    [Fact]
    public void ValidateVariantOptions_WhenEnforcedRequiredOptionIsMissing_ReturnsError()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            VariantColorDefinition(
                isRequired: true,
                isDeprecated: false,
                optionIsDeprecated: false));

        CatalogSchemaValidationResult result =
            _validator.ValidateVariantOptions(
                AttributeValueBag.Empty,
                AttributeValueBag.Empty,
                schema);

        CatalogSchemaValidationError error = Assert.Single(result.Errors);

        Assert.Equal(
            "catalog_schema.required_attribute_missing",
            error.Code);
    }

    [Fact]
    public void ValidateVariantOptions_WhenNewValueUsesDeprecatedOption_ReturnsError()
    {
        EffectiveProductTypeSchema schema = CreateSchema(
            VariantColorDefinition(
                isRequired: false,
                isDeprecated: false,
                optionIsDeprecated: true));

        CatalogSchemaValidationResult result =
            _validator.ValidateVariantOptions(
                AttributeValueBag.Empty,
                Options("space-black"),
                schema);

        CatalogSchemaValidationError error = Assert.Single(result.Errors);

        Assert.Equal(
            "catalog_schema.deprecated_option_read_only",
            error.Code);
    }

    private static EffectiveProductTypeSchema CreateSchema(
        params EffectiveAttributeDefinition[] attributes) =>
        new(
            EffectiveSchemaVersion: 1,
            Attributes: attributes);

    private static EffectiveAttributeDefinition VariantColorDefinition(
        bool isRequired,
        bool isDeprecated,
        bool optionIsDeprecated) =>
        new(
            AttributeKey.Create("color"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired,
            AttributeEnforcementStatus.Enforced,
            isDeprecated,
            MinimumLength: null,
            MaximumLength: null,
            MinimumValue: null,
            MaximumValue: null,
            MeasurementUnitFamily: null,
            Options:
            [
                new EffectiveAttributeOption(
                    AttributeOptionCode.Create("space-black"),
                    optionIsDeprecated)
            ]);

    private static AttributeValueBag Options(string color) =>
        AttributeValueBag.Empty.With(
            AttributeKey.Create("color"),
            AttributeValue.SingleSelect.Create(color));
}