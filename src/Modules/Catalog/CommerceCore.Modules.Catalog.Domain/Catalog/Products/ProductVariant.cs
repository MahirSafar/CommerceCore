using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Domain.Catalog.Products;

public sealed class ProductVariant : BaseEntity<ProductVariantId>
{
    public TenantId TenantId { get; private set; }

    private ProductVariant()
    {
    }

    private ProductVariant(
        ProductVariantId id,
        TenantId tenantId,
        VariantSku sku,
        Money price,
        AttributeValueBag options,
        bool isDefault)
        : base(id)
    {
        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID cannot be empty.",
                nameof(tenantId));
        }

        TenantId = tenantId;
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
        TenantId tenantId,
        VariantSku sku,
        Money price,
        AttributeValueBag options,
        bool isDefault)
    {
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(options);

        return new ProductVariant(
            ProductVariantId.New(),
            tenantId,
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

        if (Status == ProductVariantStatus.Active && newPrice.Amount == 0)
        {
            throw new ProductDomainException(
                "product_variant.active_price_must_be_positive",
                "The price of an active variant must be greater than zero.");
        }

        if (Price == newPrice)
            return false;

        Price = newPrice;
        return true;
    }

    internal bool Activate()
    {
        EnsureNotArchived();

        if (Status == ProductVariantStatus.Active)
            return false;

        if (Price.Amount == 0)
        {
            throw new ProductDomainException(
                "product_variant.activation_requires_price",
                "A variant with a zero price cannot be activated.");
        }

        Status = ProductVariantStatus.Active;

        return true;
    }

    internal bool Deactivate()
    {
        EnsureNotArchived();

        if (Status != ProductVariantStatus.Active)
            return false;

        Status = ProductVariantStatus.Inactive;

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