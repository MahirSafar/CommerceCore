using System.Collections.ObjectModel;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Domain.Catalog.ProductTypes;

public sealed class AttributeDefinition : BaseEntity<AttributeDefinitionId>
{
    private readonly List<AttributeOption> _options = [];
    private readonly ReadOnlyCollection<AttributeOption> _readOnlyOptions;

    public TenantId TenantId { get; private set; }

    private AttributeDefinition() => _readOnlyOptions = _options.AsReadOnly();

    private AttributeDefinition(
        AttributeDefinitionId id,
        TenantId tenantId,
        ProductTypeId productTypeId,
        AttributeKey key,
        AttributeDataType dataType,
        AttributeScope scope,
        bool isRequired,
        int displayOrder,
        decimal? minimumValue,
        decimal? maximumValue,
        int? minimumLength,
        int? maximumLength,
        MeasurementUnitFamily? measurementUnitFamily)
        : base(id)
    {
        if (productTypeId.Value == Guid.Empty)
            throw new ArgumentException("Product type ID cannot be empty.", nameof(productTypeId));

        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID cannot be empty.",
                nameof(tenantId));
        }

        TenantId = tenantId;

        ValidateDisplayOrder(displayOrder);

        ValidateScopeDataType(dataType, scope);

        ValidateConstraints(
            dataType,
            minimumValue,
            maximumValue,
            minimumLength,
            maximumLength,
            measurementUnitFamily);

        _readOnlyOptions = _options.AsReadOnly();

        ProductTypeId = productTypeId;
        Key = key;
        DataType = dataType;
        Scope = scope;
        IsRequired = isRequired;
        DisplayOrder = displayOrder;
        MinimumValue = minimumValue;
        MaximumValue = maximumValue;
        MinimumLength = minimumLength;
        MaximumLength = maximumLength;
        MeasurementUnitFamily = measurementUnitFamily;

        EnforcementStatus = isRequired
            ? AttributeEnforcementStatus.Draft
            : AttributeEnforcementStatus.Enforced;
    }

    public ProductTypeId ProductTypeId { get; private set; }

    public AttributeKey Key { get; private set; }

    public AttributeDataType DataType { get; private set; }

    public AttributeScope Scope { get; private set; }

    public bool IsRequired { get; private set; }

    public AttributeEnforcementStatus EnforcementStatus { get; private set; }

    public bool IsDeprecated { get; private set; }

    public int DisplayOrder { get; private set; }

    public decimal? MinimumValue { get; private set; }

    public decimal? MaximumValue { get; private set; }

    public int? MinimumLength { get; private set; }

    public int? MaximumLength { get; private set; }

    public MeasurementUnitFamily? MeasurementUnitFamily { get; private set; }

    public IReadOnlyCollection<AttributeOption> Options => _readOnlyOptions;

    public static AttributeDefinition Create(
        TenantId tenantId,
        ProductTypeId productTypeId,
        AttributeKey key,
        AttributeDataType dataType,
        AttributeScope scope,
        bool isRequired,
        int displayOrder,
        decimal? minimumValue = null,
        decimal? maximumValue = null,
        int? minimumLength = null,
        int? maximumLength = null,
        MeasurementUnitFamily? measurementUnitFamily = null)
    {
        return new AttributeDefinition(
            AttributeDefinitionId.New(),
            tenantId,
            productTypeId,
            key,
            dataType,
            scope,
            isRequired,
            displayOrder,
            minimumValue,
            maximumValue,
            minimumLength,
            maximumLength,
            measurementUnitFamily);
    }

    public AttributeOption AddOption(
        AttributeOptionCode code,
        int displayOrder)
    {
        EnsureSelectable();
        EnsureNotDeprecated();
        ValidateDisplayOrder(displayOrder);

        if (_options.Any(option => option.Code == code))
        {
            throw new ProductTypeDomainException(
                "attribute_definition.duplicate_option_code",
                $"Option code '{code}' already exists for attribute '{Key}'.");
        }

        if (_options.Any(option => option.DisplayOrder == displayOrder))
        {
            throw new ProductTypeDomainException(
                "attribute_definition.duplicate_option_display_order",
                $"Display order '{displayOrder}' is already used by an option of attribute '{Key}'.");
        }

        AttributeOption option = AttributeOption.Create(TenantId, Id, code, displayOrder);
        _options.Add(option);

        return option;
    }

    public bool DeprecateOption(AttributeOptionId optionId)
    {
        AttributeOption? option = _options.SingleOrDefault(item => item.Id == optionId);

        return option is null
            ? throw new ProductTypeDomainException(
                "attribute_definition.option_not_found",
                $"Option '{optionId}' does not belong to attribute '{Key}'.")
            : option.Deprecate();
    }

    public void BeginBackfilling()
    {
        EnsureRequired();
        EnsureNotDeprecated();

        if (EnforcementStatus != AttributeEnforcementStatus.Draft)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.invalid_enforcement_transition",
                "Only a Draft attribute can transition to Backfilling.");
        }

        EnforcementStatus = AttributeEnforcementStatus.Backfilling;
    }

    public void Enforce(bool allExistingProductsComply)
    {
        EnsureRequired();
        EnsureNotDeprecated();

        if (EnforcementStatus != AttributeEnforcementStatus.Backfilling)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.invalid_enforcement_transition",
                "Only a Backfilling attribute can transition to Enforced.");
        }

        if (!allExistingProductsComply)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.backfill_incomplete",
                "A required attribute cannot be enforced until all existing products comply.");
        }

        EnforcementStatus = AttributeEnforcementStatus.Enforced;
    }

    public bool MakeOptional()
    {
        if (!IsRequired)
            return false;

        IsRequired = false;
        EnforcementStatus = AttributeEnforcementStatus.Enforced;

        return true;
    }

    public bool MakeRequired()
    {
        EnsureNotDeprecated();

        if (IsRequired)
            return false;

        IsRequired = true;
        EnforcementStatus = AttributeEnforcementStatus.Draft;

        return true;
    }

    public bool Deprecate()
    {
        if (IsDeprecated)
            return false;

        if (IsRequired)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.required_attribute_cannot_be_deprecated",
                "A required attribute must be made optional before it can be deprecated.");
        }

        IsDeprecated = true;

        return true;
    }

    private void EnsureSelectable()
    {
        if (DataType is AttributeDataType.SingleSelect or AttributeDataType.MultiSelect)
            return;

        throw new ProductTypeDomainException(
            "attribute_definition.options_not_supported",
            $"Attribute '{Key}' with data type '{DataType}' cannot have options.");
    }

    private void EnsureRequired()
    {
        if (!IsRequired)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.not_required",
                "Only a required attribute can have an enforcement lifecycle.");
        }
    }

    private void EnsureNotDeprecated()
    {
        if (IsDeprecated)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.deprecated",
                $"Attribute '{Key}' is deprecated and cannot be changed.");
        }
    }

    private static void ValidateScopeDataType(
        AttributeDataType dataType,
        AttributeScope scope)
    {
        if (scope != AttributeScope.VariantOption)
            return;

        if (dataType == AttributeDataType.SingleSelect)
            return;

        throw new ProductTypeDomainException(
            "attribute_definition.variant_option_must_be_single_select",
            "A variant option attribute must use the SingleSelect data type.");
    }

    private static void ValidateDisplayOrder(int displayOrder)
    {
        if (displayOrder < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayOrder),
                "Display order cannot be negative.");
        }
    }

    private static void ValidateConstraints(
        AttributeDataType dataType,
        decimal? minimumValue,
        decimal? maximumValue,
        int? minimumLength,
        int? maximumLength,
        MeasurementUnitFamily? measurementUnitFamily)
    {
        var supportsNumericRange = dataType is
            AttributeDataType.Integer or
            AttributeDataType.Decimal or
            AttributeDataType.Measurement;

        if (!supportsNumericRange &&
            (minimumValue.HasValue || maximumValue.HasValue))
        {
            throw new ProductTypeDomainException(
                "attribute_definition.numeric_range_not_supported",
                $"Data type '{dataType}' does not support numeric range constraints.");
        }

        if (minimumValue.HasValue &&
            maximumValue.HasValue &&
            minimumValue.Value > maximumValue.Value)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.invalid_numeric_range",
                "Minimum value cannot be greater than maximum value.");
        }

        if (dataType == AttributeDataType.Integer &&
            ((minimumValue.HasValue && decimal.Truncate(minimumValue.Value) != minimumValue.Value) ||
             (maximumValue.HasValue && decimal.Truncate(maximumValue.Value) != maximumValue.Value)))
        {
            throw new ProductTypeDomainException(
                "attribute_definition.integer_range_must_be_integral",
                "Integer attribute range values must be whole numbers.");
        }

        if (dataType != AttributeDataType.Text &&
            (minimumLength.HasValue || maximumLength.HasValue))
        {
            throw new ProductTypeDomainException(
                "attribute_definition.length_constraint_not_supported",
                $"Data type '{dataType}' does not support text length constraints.");
        }

        if ((minimumLength ?? 0) < 0 || (maximumLength ?? 0) < 0)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.invalid_length",
                "Text length constraints cannot be negative.");
        }

        if (minimumLength.HasValue &&
            maximumLength.HasValue &&
            minimumLength.Value > maximumLength.Value)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.invalid_length_range",
                "Minimum length cannot be greater than maximum length.");
        }

        if (dataType == AttributeDataType.Measurement &&
            !measurementUnitFamily.HasValue)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.measurement_unit_family_required",
                "A Measurement attribute requires a measurement unit family.");
        }

        if (dataType != AttributeDataType.Measurement &&
            measurementUnitFamily.HasValue)
        {
            throw new ProductTypeDomainException(
                "attribute_definition.measurement_unit_family_not_supported",
                $"Data type '{dataType}' cannot have a measurement unit family.");
        }
    }
}