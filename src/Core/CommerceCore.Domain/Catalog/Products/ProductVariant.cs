using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Domain.Common.ValueObjects;

namespace CommerceCore.Domain.Catalog.Products;

public sealed class ProductVariant : BaseEntity<ProductVariantId>
{
    private ProductVariant()
    {
    }

    private ProductVariant(
        ProductVariantId id,
        VariantSku sku,
        Money price,
        AttributeValueBag options,
        bool isDefault)
        : base(id)
    {
        Sku = sku;
        Price = price;
        Options = options;
        IsDefault = isDefault;
        Status = ProductVariantStatus.Draft;
    }

    public VariantSku Sku { get; private set; }

    public Money Price { get; private set; } = null!;

    public AttributeValueBag Options { get; private set; } =
        AttributeValueBag.Empty;

    public bool IsDefault { get; private set; }

    public ProductVariantStatus Status { get; private set; }

    internal static ProductVariant Create(
        VariantSku sku,
        Money price,
        AttributeValueBag options,
        bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(options);

        return new ProductVariant(
            ProductVariantId.New(),
            sku,
            price,
            options,
            isDefault);
    }

    internal bool SetDefault(bool isDefault)
    {
        if (IsDefault == isDefault)
            return false;

        IsDefault = isDefault;
        return true;
    }

    public bool ChangePrice(Money newPrice)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(newPrice);

        if (!string.Equals(
                Price.Currency,
                newPrice.Currency,
                StringComparison.Ordinal))
        {
            throw new ProductDomainException(
                "product_variant.currency_change_not_allowed",
                "A variant's currency cannot be changed.");
        }

        if (Price == newPrice)
            return false;

        Price = newPrice;
        return true;
    }

    private void EnsureNotArchived()
    {
        if (Status == ProductVariantStatus.Archived)
        {
            throw new ProductDomainException(
                "product_variant.archived",
                "No changes can be made to an archived variant.");
        }
    }
}