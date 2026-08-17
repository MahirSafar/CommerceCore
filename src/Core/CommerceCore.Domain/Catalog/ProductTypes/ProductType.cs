using System.Collections.ObjectModel;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;

namespace CommerceCore.Domain.Catalog.ProductTypes;

public sealed class ProductType : AggregateRoot<ProductTypeId>
{
    private readonly List<AttributeDefinition> _attributeDefinitions = [];
    private readonly ReadOnlyCollection<AttributeDefinition> _readOnlyAttributeDefinitions;

    private ProductType() =>
        _readOnlyAttributeDefinitions = _attributeDefinitions.AsReadOnly();

    private ProductType(
        ProductTypeId id,
        ProductTypeCode code,
        ProductTypeId? parentProductTypeId,
        bool isAssignable)
        : base(id)
    {
        if (parentProductTypeId.HasValue &&
            parentProductTypeId.Value.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Parent product type ID cannot be empty.",
                nameof(parentProductTypeId));
        }

        _readOnlyAttributeDefinitions = _attributeDefinitions.AsReadOnly();

        Code = code;
        ParentProductTypeId = parentProductTypeId;
        IsAssignable = isAssignable;

        SchemaVersion = 0;
    }

    public ProductTypeCode Code { get; private set; }

    public ProductTypeId? ParentProductTypeId { get; private set; }

    public bool IsAssignable { get; private set; }

    public long SchemaVersion { get; private set; }

    public IReadOnlyCollection<AttributeDefinition> AttributeDefinitions =>
        _readOnlyAttributeDefinitions;

    public static ProductType CreateRoot(
        ProductTypeCode code,
        bool isAssignable = false) =>
        new(
            ProductTypeId.New(),
            code,
            parentProductTypeId: null,
            isAssignable);


    public static ProductType CreateChild(
        ProductTypeId parentProductTypeId,
        ProductTypeCode code,
        bool isAssignable = true) => parentProductTypeId.Value == Guid.Empty
            ? throw new ArgumentException(
                "Parent product type ID cannot be empty.",
                nameof(parentProductTypeId))
            : new ProductType(
                ProductTypeId.New(),
                code,
                parentProductTypeId,
                isAssignable);
    

    public bool EnableAssignments()
    {
        if (IsAssignable)
            return false;

        IsAssignable = true;
        return true;
    }

    public bool DisableAssignments()
    {
        if (!IsAssignable)
            return false;

        IsAssignable = false;
        return true;
    }

    public AttributeDefinition DefineAttribute(
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
        if (_attributeDefinitions.Any(item => item.Key == key))
        {
            throw new ProductTypeDomainException(
                "product_type.duplicate_attribute_key",
                $"Attribute key '{key}' already exists on product type '{Code}'.");
        }

        if (_attributeDefinitions.Any(item => item.DisplayOrder == displayOrder))
        {
            throw new ProductTypeDomainException(
                "product_type.duplicate_attribute_display_order",
                $"Display order '{displayOrder}' is already used by product type '{Code}'.");
        }

        AttributeDefinition definition = AttributeDefinition.Create(
            Id,
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

        _attributeDefinitions.Add(definition);

        return definition;
    }

    public AttributeOption AddAttributeOption(
        AttributeDefinitionId attributeDefinitionId,
        AttributeOptionCode code,
        int displayOrder) =>
        GetAttributeDefinition(attributeDefinitionId)
            .AddOption(code, displayOrder);

    public bool DeprecateAttributeOption(
        AttributeDefinitionId attributeDefinitionId,
        AttributeOptionId attributeOptionId) =>
        GetAttributeDefinition(attributeDefinitionId)
            .DeprecateOption(attributeOptionId);

    public void BeginAttributeBackfilling(
        AttributeDefinitionId attributeDefinitionId) =>
        GetAttributeDefinition(attributeDefinitionId)
            .BeginBackfilling();

    public void EnforceAttribute(
        AttributeDefinitionId attributeDefinitionId,
        bool allExistingProductsComply) =>
        GetAttributeDefinition(attributeDefinitionId)
            .Enforce(allExistingProductsComply);

    public bool MakeAttributeOptional(
        AttributeDefinitionId attributeDefinitionId) =>
        GetAttributeDefinition(attributeDefinitionId)
            .MakeOptional();

    public bool MakeAttributeRequired(
        AttributeDefinitionId attributeDefinitionId) =>
        GetAttributeDefinition(attributeDefinitionId)
            .MakeRequired();

    public bool DeprecateAttribute(
        AttributeDefinitionId attributeDefinitionId) =>
        GetAttributeDefinition(attributeDefinitionId)
            .Deprecate();

    private AttributeDefinition GetAttributeDefinition(
        AttributeDefinitionId attributeDefinitionId) =>
        _attributeDefinitions.SingleOrDefault(
            item => item.Id == attributeDefinitionId) ?? throw new ProductTypeDomainException(
            "product_type.attribute_not_found",
            $"Attribute '{attributeDefinitionId}' does not belong to product type '{Code}'.");
}