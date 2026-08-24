using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Catalog.Products;

public sealed class ProductVariantTests
{
    [Fact]
    public void AddVariant_WithFirstDefaultVariant_AddsDraftVariant()
    {
        Product product = CreateProduct();

        ProductVariant variant = product.AddVariant(
            VariantSku.Create("laptop-black-16"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductVariant item = Assert.Single(product.Variants);
        Assert.Equal(variant, item);
        Assert.Equal("LAPTOP-BLACK-16", variant.Sku.Value);
        Assert.Equal(1200m, variant.Price.Amount);
        Assert.True(variant.IsDefault);
        Assert.Equal(ProductVariantStatus.Draft, variant.Status);
    }

    [Fact]
    public void AddVariant_WhenFirstVariantIsNotDefault_ThrowsDomainException()
    {
        Product product = CreateProduct();

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.AddVariant(
                VariantSku.Create("laptop-black-16"),
                Money.Create(1200m, "USD"),
                Options("space-black"),
                isDefault: false));

        Assert.Equal(
            "product.first_variant_must_be_default",
            exception.Code);
    }

    [Fact]
    public void AddVariant_WithDuplicateSku_ThrowsDomainException()
    {
        Product product = CreateProduct();

        product.AddVariant(
            VariantSku.Create("laptop-black-16"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.AddVariant(
                VariantSku.Create("LAPTOP-BLACK-16"),
                Money.Create(1300m, "USD"),
                Options("silver"),
                isDefault: false));

        Assert.Equal("product.variant_sku_duplicate", exception.Code);
    }

    [Fact]
    public void AddVariant_WithDuplicateOptions_ThrowsDomainException()
    {
        Product product = CreateProduct();

        product.AddVariant(
            VariantSku.Create("laptop-black-16"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.AddVariant(
                VariantSku.Create("laptop-black-32"),
                Money.Create(1300m, "USD"),
                Options("space-black"),
                isDefault: false));

        Assert.Equal(
            "product.variant_options_duplicate",
            exception.Code);
    }

    [Fact]
    public void AddVariant_WhenSecondDefaultExists_ThrowsDomainException()
    {
        Product product = CreateProduct();

        product.AddVariant(
            VariantSku.Create("laptop-black-16"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.AddVariant(
                VariantSku.Create("laptop-silver-16"),
                Money.Create(1200m, "USD"),
                Options("silver"),
                isDefault: true));

        Assert.Equal(
            "product.default_variant_already_exists",
            exception.Code);
    }

    [Fact]
    public void SetDefaultVariant_SwitchesDefaultVariant()
    {
        Product product = CreateProduct();

        ProductVariant black = product.AddVariant(
            VariantSku.Create("laptop-black-16"),
            Money.Create(1200m, "USD"),
            Options("space-black"),
            isDefault: true);

        ProductVariant silver = product.AddVariant(
            VariantSku.Create("laptop-silver-16"),
            Money.Create(1200m, "USD"),
            Options("silver"),
            isDefault: false);

        bool changed = product.SetDefaultVariant(silver.Id);

        Assert.True(changed);
        Assert.False(black.IsDefault);
        Assert.True(silver.IsDefault);
        Assert.Equal(
            silver.Id,
            Assert.Single(product.Variants, variant => variant.IsDefault).Id);
    }

    [Fact]
    public void AddVariant_WhenProductIsArchived_ThrowsDomainException()
    {
        Product product = CreateProduct();

        product.Archive(
            new DateTimeOffset(2026, 8, 24, 10, 0, 0, TimeSpan.Zero),
            "test");

        ProductDomainException exception = Assert.Throws<
            ProductDomainException>(() => product.AddVariant(
                VariantSku.Create("laptop-black-16"),
                Money.Create(1200m, "USD"),
                Options("space-black"),
                isDefault: true));

        Assert.Equal("product.archived", exception.Code);
    }

    private static Product CreateProduct()
    {
        LanguageCode language = LanguageCode.Create("en");

        LocalizedText name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Variant test product")
            ]);

        return Product.Create(
            name,
            Money.Create(1000m, "USD"),
            ProductTypeId.New(),
            new DateTimeOffset(
                2026, 8, 24, 9, 0, 0, TimeSpan.Zero));
    }

    private static AttributeValueBag Options(string color) =>
        AttributeValueBag.Empty.With(
            AttributeKey.Create("color"),
            AttributeValue.SingleSelect.Create(color));
}