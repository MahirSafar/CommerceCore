using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Platform.Contracts;
using System.Collections.ObjectModel;

namespace CommerceCore.Domain.Catalog.Products;

public sealed class Product : SoftDeletableAggregateRoot<ProductId>
{
    public const int MaximumNameLength = 200;

    private readonly List<ProductVariant> _variants = [];

    private readonly ReadOnlyCollection<ProductVariant> _readOnlyVariants;

    public TenantId TenantId { get; private set; }

    public LocalizedText Name { get; private set; } = null!;

    public Money Price { get; private set; } = null!;

    public ProductTypeId ProductTypeId { get; private set; }

    public AttributeValueBag Specifications { get; private set; } = AttributeValueBag.Empty;

    public long ValidatedAgainstVersion { get; private set; }

    public ProductStatus Status { get; private set; }

    public IReadOnlyCollection<ProductVariant> Variants => _readOnlyVariants;

    private Product()
    {
        _readOnlyVariants = _variants.AsReadOnly();
    }

    private Product(
        ProductId id,
        TenantId tenantId,
        LocalizedText name,
        Money price,
        ProductTypeId productTypeId)
        : base(id)
    {
        if (productTypeId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Product type ID cannot be empty.",
                nameof(productTypeId));
        }

        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID cannot be empty.",
                nameof(tenantId));
        }

        TenantId = tenantId;
        Name = name;
        Price = price;
        ProductTypeId = productTypeId;
        Specifications = AttributeValueBag.Empty;
        ValidatedAgainstVersion = 0;
        Status = ProductStatus.Draft;
        _readOnlyVariants = _variants.AsReadOnly();
    }

    public static Product Create(
        TenantId tenantId,
        LocalizedText name,
        Money price,
        ProductTypeId productTypeId,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(price);

        if (createdAtUtc == default)
        {
            throw new ArgumentOutOfRangeException(
                nameof(createdAtUtc),
                "Created-at timestamp cannot be empty.");
        }

        EnsureValidName(name);

        Product product = new(
            ProductId.New(),
            tenantId,
            name,
            price,
            productTypeId);

        product.RaiseDomainEvent(
            new ProductCreatedDomainEvent(
                product.Id,
                createdAtUtc));

        return product;
    }

    public bool ChangeName(LocalizedText newName)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(newName);

        EnsureValidName(newName);

        if (Name.Equals(newName))
            return false;

        Name = newName;
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
                "product.currency_change_not_allowed",
                "A product's base currency cannot be changed.");
        }

        if (Status == ProductStatus.Active && newPrice.Amount == 0)
        {
            throw new ProductDomainException(
                "product.active_price_must_be_positive",
                "The price of an active product must be greater than zero.");
        }

        if (Price == newPrice)
            return false;

        Price = newPrice;
        return true;
    }

    public bool Activate()
    {
        EnsureNotArchived();

        if (Status == ProductStatus.Active)
            return false;

        if (!_variants.Any(
                variant => variant.IsDefault &&
                           variant.Status == ProductVariantStatus.Active))
        {
            throw new ProductDomainException(
                "product.activation_requires_active_default_variant",
                "A product requires an active default variant before activation.");
        }

        Status = ProductStatus.Active;
        return true;
    }

    public bool Deactivate()
    {
        EnsureNotArchived();

        if (Status != ProductStatus.Active)
            return false;

        Status = ProductStatus.Inactive;
        return true;
    }

    public bool Archive(DateTimeOffset archivedAtUtc, string? archivedBy)
    {
        if (!MarkAsDeletedCore(archivedAtUtc, archivedBy))
            return false;

        RaiseDomainEvent(
            new ProductArchivedDomainEvent(
                Id,
                DeletedAtUtc!.Value,
                archivedBy));

        return true;
    }

    public bool Restore()
    {
        if (!RestoreCore())
            return false;

        if (Status == ProductStatus.Active)
            Status = ProductStatus.Inactive;

        return true;
    }

    public ProductVariant AddVariant(
        VariantSku sku,
        Money price,
        AttributeValueBag options,
        bool isDefault)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(price);
        ArgumentNullException.ThrowIfNull(options);

        if (_variants.Any(variant => variant.Sku == sku))
        {
            throw new ProductDomainException(
                "product.variant_sku_duplicate",
                $"SKU '{sku.Value}' already exists on this product.");
        }

        if (_variants.Any(variant => variant.Options.Equals(options)))
        {
            throw new ProductDomainException(
                "product.variant_options_duplicate",
                "A variant with the same option combination already exists.");
        }

        if (_variants.Count == 0 && !isDefault)
        {
            throw new ProductDomainException(
                "product.first_variant_must_be_default",
                "The first variant of a product must be the default variant.");
        }

        if (isDefault && _variants.Any(variant => variant.IsDefault))
        {
            throw new ProductDomainException(
                "product.default_variant_already_exists",
                "A product can have only one default variant.");
        }

        ProductVariant variant = ProductVariant.Create(
            TenantId,
            sku,
            price,
            options,
            isDefault);

        _variants.Add(variant);

        return variant;
    }

    public bool ActivateVariant(ProductVariantId variantId)
    {
        EnsureNotArchived();

        return GetVariant(variantId).Activate();
    }

    public bool DeactivateVariant(ProductVariantId variantId)
    {
        EnsureNotArchived();

        ProductVariant variant = GetVariant(variantId);

        if (Status == ProductStatus.Active &&
            variant.IsDefault &&
            variant.Status == ProductVariantStatus.Active)
        {
            throw new ProductDomainException(
                "product.active_default_variant_cannot_be_deactivated",
                "Set another active variant as default or deactivate the product first.");
        }

        return variant.Deactivate();
    }

    public bool SetDefaultVariant(ProductVariantId variantId)
    {
        EnsureNotArchived();

        ProductVariant variant = GetVariant(variantId);

        if (Status == ProductStatus.Active &&
            variant.Status != ProductVariantStatus.Active)
        {
            throw new ProductDomainException(
                "product.active_default_variant_must_be_active",
                "An active product's default variant must be active.");
        }

        if (variant.IsDefault)
            return false;

        foreach (ProductVariant item in _variants)
            item.SetDefault(false);

        variant.SetDefault(true);

        return true;
    }

    public bool ApplyValidatedSpecifications(
    AttributeValueBag specifications,
    long effectiveSchemaVersion)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(specifications);

        if (effectiveSchemaVersion <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(effectiveSchemaVersion),
                "The effective schema version must be positive.");
        }

        if (Specifications.Equals(specifications) &&
            ValidatedAgainstVersion == effectiveSchemaVersion)
        {
            return false;
        }

        Specifications = specifications;
        ValidatedAgainstVersion = effectiveSchemaVersion;

        return true;
    }

    private ProductVariant GetVariant(ProductVariantId variantId)
    {
        if (variantId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Product variant ID cannot be empty.",
                nameof(variantId));
        }

        return _variants.SingleOrDefault(
            item => item.Id == variantId)
            ?? throw new ProductDomainException(
                "product.variant_not_found",
                $"Variant '{variantId}' was not found on this product.");
    }

    private static void EnsureValidName(LocalizedText name)
    {
        if (name.Translations.Values.Any(
                value => value.Length > MaximumNameLength))
        {
            throw new ProductDomainException(
                "product.name_too_long",
                $"Each product-name translation must not exceed " +
                $"{MaximumNameLength} characters.");
        }
    }

    private void EnsureNotArchived()
    {
        if (IsDeleted)
        {
            throw new ProductDomainException(
                "product.archived",
                "No changes can be made to an archived product.");
        }
    }
}