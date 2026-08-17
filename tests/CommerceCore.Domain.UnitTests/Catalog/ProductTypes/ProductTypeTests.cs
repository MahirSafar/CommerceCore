using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes;

public sealed class ProductTypeTests
{
    [Fact]
    public void CreateRoot_CreatesNonAssignableRootWithoutParent()
    {
        ProductType productType = ProductType.CreateRoot(
            ProductTypeCode.Create("electronics"));

        Assert.Equal("electronics", productType.Code.Value);
        Assert.Null(productType.ParentProductTypeId);
        Assert.False(productType.IsAssignable);
        Assert.Equal(0, productType.SchemaVersion);
    }

    [Fact]
    public void CreateChild_CreatesAssignableTypeWithParent()
    {
        ProductTypeId parentId = ProductTypeId.New();

        ProductType productType = ProductType.CreateChild(
            parentId,
            ProductTypeCode.Create("gaming_laptop"));

        Assert.Equal(parentId, productType.ParentProductTypeId);
        Assert.True(productType.IsAssignable);
    }

    [Fact]
    public void DefineAttribute_AddsAttributeToProductType()
    {
        ProductType productType = CreateProductType();

        AttributeDefinition attribute = productType.DefineAttribute(
            AttributeKey.Create("ram_gb"),
            AttributeDataType.Integer,
            AttributeScope.ProductSpecification,
            isRequired: true,
            displayOrder: 0,
            minimumValue: 1,
            maximumValue: 1024);

        Assert.Single(productType.AttributeDefinitions);
        Assert.Equal(productType.Id, attribute.ProductTypeId);
        Assert.Equal("ram_gb", attribute.Key.Value);
        Assert.Equal(AttributeEnforcementStatus.Draft, attribute.EnforcementStatus);
    }

    [Fact]
    public void DefineAttribute_WithDuplicateKey_ThrowsDomainException()
    {
        ProductType productType = CreateProductType();

        productType.DefineAttribute(
            AttributeKey.Create("color"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired: true,
            displayOrder: 0);

        ProductTypeDomainException exception = Assert.Throws<ProductTypeDomainException>(() =>
            productType.DefineAttribute(
                AttributeKey.Create("color"),
                AttributeDataType.SingleSelect,
                AttributeScope.VariantOption,
                isRequired: false,
                displayOrder: 1));

        Assert.Equal("product_type.duplicate_attribute_key", exception.Code);
    }

    [Fact]
    public void AddAttributeOption_DelegatesToOwnedAttribute()
    {
        ProductType productType = CreateProductType();

        AttributeDefinition attribute = productType.DefineAttribute(
            AttributeKey.Create("color"),
            AttributeDataType.SingleSelect,
            AttributeScope.VariantOption,
            isRequired: true,
            displayOrder: 0);

        AttributeOption option = productType.AddAttributeOption(
            attribute.Id,
            AttributeOptionCode.Create("space-black"),
            displayOrder: 0);

        Assert.Single(attribute.Options);
        Assert.Equal("space-black", option.Code.Value);
    }

    [Fact]
    public void EnforceAttribute_AfterBackfill_EnforcesAttribute()
    {
        ProductType productType = CreateProductType();

        AttributeDefinition attribute = productType.DefineAttribute(
            AttributeKey.Create("ram_gb"),
            AttributeDataType.Integer,
            AttributeScope.ProductSpecification,
            isRequired: true,
            displayOrder: 0);

        productType.BeginAttributeBackfilling(attribute.Id);
        productType.EnforceAttribute(
            attribute.Id,
            allExistingProductsComply: true);

        Assert.Equal(
            AttributeEnforcementStatus.Enforced,
            attribute.EnforcementStatus);
    }

    [Fact]
    public void DisableAssignments_IsIdempotent()
    {
        ProductType productType = ProductType.CreateRoot(
            ProductTypeCode.Create("coffee"),
            isAssignable: true);

        Assert.True(productType.DisableAssignments());
        Assert.False(productType.DisableAssignments());
        Assert.False(productType.IsAssignable);
    }

    private static ProductType CreateProductType() =>
        ProductType.CreateRoot(
            ProductTypeCode.Create("laptop"),
            isAssignable: true);
}