using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes;

public sealed class AttributeDefinitionTests
{
    [Fact]
    public void Create_EmptyTenantId_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            AttributeDefinition.Create(
                default,
                ProductTypeId.New(),
                AttributeKey.Create("sample_attribute"),
                AttributeDataType.Text,
                AttributeScope.ProductSpecification,
                isRequired: false,
                displayOrder: 0));
    }

    [Fact]
    public void Create_RequiredAttribute_StartsInDraft()
    {
        AttributeDefinition definition = Create(isRequired: true);

        Assert.True(definition.IsRequired);
        Assert.Equal(AttributeEnforcementStatus.Draft, definition.EnforcementStatus);
    }

    [Fact]
    public void Create_OptionalAttribute_StartsEnforced()
    {
        AttributeDefinition definition = Create(isRequired: false);

        Assert.False(definition.IsRequired);
        Assert.Equal(AttributeEnforcementStatus.Enforced, definition.EnforcementStatus);
    }

    [Fact]
    public void Create_VariantOptionWithSingleSelect_Succeeds()
    {
        AttributeDefinition definition = Create(
            dataType: AttributeDataType.SingleSelect,
            scope: AttributeScope.VariantOption);

        Assert.Equal(AttributeScope.VariantOption, definition.Scope);
        Assert.Equal(AttributeDataType.SingleSelect, definition.DataType);
    }

    [Theory]
    [InlineData(AttributeDataType.Text)]
    [InlineData(AttributeDataType.Integer)]
    [InlineData(AttributeDataType.Decimal)]
    [InlineData(AttributeDataType.Boolean)]
    [InlineData(AttributeDataType.MultiSelect)]
    [InlineData(AttributeDataType.Measurement)]
    public void Create_VariantOptionWithNonSingleSelect_ThrowsDomainException(
        AttributeDataType dataType)
    {
        ProductTypeDomainException exception = Assert.Throws<
            ProductTypeDomainException>(() => Create(
                dataType: dataType,
                scope: AttributeScope.VariantOption,
                measurementUnitFamily:
                    dataType == AttributeDataType.Measurement
                        ? MeasurementUnitFamily.Create("mass")
                        : null));

        Assert.Equal(
            "attribute_definition.variant_option_must_be_single_select",
            exception.Code);
    }

    [Fact]
    public void AddOption_ForSingleSelect_AddsOption()
    {
        AttributeDefinition definition = Create(dataType: AttributeDataType.SingleSelect);

        AttributeOption option = definition.AddOption(
            AttributeOptionCode.Create("space-black"),
            displayOrder: 0);

        Assert.Single(definition.Options);
        Assert.Equal(definition.Id, option.AttributeDefinitionId);
        Assert.Equal("space-black", option.Code.Value);
    }

    [Fact]
    public void AddOption_ForTextAttribute_ThrowsDomainException()
    {
        AttributeDefinition definition = Create(dataType: AttributeDataType.Text);

        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            definition.AddOption(AttributeOptionCode.Create("black"), 0));

        Assert.Equal("attribute_definition.options_not_supported", exception.Code);
    }

    [Fact]
    public void AddOption_WithDuplicateCode_ThrowsDomainException()
    {
        AttributeDefinition definition = Create(dataType: AttributeDataType.SingleSelect);

        definition.AddOption(AttributeOptionCode.Create("black"), 0);

        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            definition.AddOption(AttributeOptionCode.Create("black"), 1));

        Assert.Equal("attribute_definition.duplicate_option_code", exception.Code);
    }

    [Fact]
    public void Create_MeasurementWithoutUnitFamily_ThrowsDomainException()
    {
        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            Create(dataType: AttributeDataType.Measurement));

        Assert.Equal(
            "attribute_definition.measurement_unit_family_required",
            exception.Code);
    }

    [Fact]
    public void Create_MeasurementWithUnitFamily_Succeeds()
    {
        AttributeDefinition definition = Create(
            dataType: AttributeDataType.Measurement,
            measurementUnitFamily: MeasurementUnitFamily.Create("mass"));

        Assert.Equal("mass", definition.MeasurementUnitFamily!.Value.Value);
    }

    [Fact]
    public void Create_TextWithInvalidLengthRange_ThrowsDomainException()
    {
        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            Create(
                dataType: AttributeDataType.Text,
                minimumLength: 100,
                maximumLength: 10));

        Assert.Equal("attribute_definition.invalid_length_range", exception.Code);
    }

    [Fact]
    public void Create_IntegerWithFractionalRange_ThrowsDomainException()
    {
        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            Create(
                dataType: AttributeDataType.Integer,
                minimumValue: 1.5m));

        Assert.Equal(
            "attribute_definition.integer_range_must_be_integral",
            exception.Code);
    }

    [Fact]
    public void RequiredAttribute_CanTransitionFromDraftToEnforced()
    {
        AttributeDefinition definition = Create(isRequired: true);

        definition.BeginBackfilling();
        definition.Enforce(allExistingProductsComply: true);

        Assert.Equal(AttributeEnforcementStatus.Enforced, definition.EnforcementStatus);
    }

    [Fact]
    public void Enforce_WhenBackfillIsIncomplete_ThrowsDomainException()
    {
        AttributeDefinition definition = Create(isRequired: true);
        definition.BeginBackfilling();

        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            definition.Enforce(allExistingProductsComply: false));

        Assert.Equal("attribute_definition.backfill_incomplete", exception.Code);
    }

    [Fact]
    public void Deprecate_RequiredAttribute_RequiresItToBeOptionalFirst()
    {
        AttributeDefinition definition = Create(isRequired: true);

        Assert.Throws<ProductTypeDomainException>(() => definition.Deprecate());

        definition.MakeOptional();

        Assert.True(definition.Deprecate());
        Assert.True(definition.IsDeprecated);
    }

    private static AttributeDefinition Create(
        AttributeDataType dataType = AttributeDataType.Text,
        AttributeScope scope = AttributeScope.ProductSpecification, 
        bool isRequired = false,
        decimal? minimumValue = null,
        decimal? maximumValue = null,
        int? minimumLength = null,
        int? maximumLength = null,
        MeasurementUnitFamily? measurementUnitFamily = null)
    {
        return AttributeDefinition.Create(
            TenantId.New(),
            ProductTypeId.New(),
            AttributeKey.Create("sample_attribute"),
            dataType,
            scope,
            isRequired,
            displayOrder: 0,
            minimumValue,
            maximumValue,
            minimumLength,
            maximumLength,
            measurementUnitFamily);
    }
}