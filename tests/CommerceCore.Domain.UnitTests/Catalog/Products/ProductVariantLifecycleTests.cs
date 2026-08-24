using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Catalog.Products;

public sealed class ProductVariantLifecycleTests
{
    [Fact]
    public void Activate_WhenNoVariantIsActive_ThrowsDomainException()
    {
        Product product = CreateProduct();

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.Activate());

        Assert.Equal(
            "product.activation_requires_active_variant",
            exception.Code);
    }

    [Fact]
    public void ActivateVariant_ThenActivateProduct_ActivatesBoth()
    {
        Product product = CreateProduct();

        ProductVariant variant = product.AddVariant(
            VariantSku.Create("laptop-black"),
            Money.Create(100m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        Assert.True(product.ActivateVariant(variant.Id));
        Assert.True(product.Activate());

        Assert.Equal(ProductVariantStatus.Active, variant.Status);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void ActivateVariant_WithZeroPrice_ThrowsDomainException()
    {
        Product product = CreateProduct();

        ProductVariant variant = product.AddVariant(
            VariantSku.Create("laptop-black"),
            Money.Create(0m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() =>
                product.ActivateVariant(variant.Id));

        Assert.Equal(
            "product_variant.activation_requires_price",
            exception.Code);
    }

    [Fact]
    public void DeactivateVariant_WhenItIsProductLastActiveVariant_ThrowsDomainException()
    {
        Product product = CreateProduct();

        ProductVariant variant = product.AddVariant(
            VariantSku.Create("laptop-black"),
            Money.Create(100m, "USD"),
            AttributeValueBag.Empty,
            isDefault: true);

        product.ActivateVariant(variant.Id);
        product.Activate();

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() =>
                product.DeactivateVariant(variant.Id));

        Assert.Equal(
            "product.last_active_variant_cannot_be_deactivated",
            exception.Code);
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Variant lifecycle product")
            ]);

        return Product.Create(
            name,
            Money.Create(100m, "USD"),
            ProductTypeId.New(),
            new DateTimeOffset(
                2026, 8, 24, 18, 0, 0, TimeSpan.Zero));
    }
}