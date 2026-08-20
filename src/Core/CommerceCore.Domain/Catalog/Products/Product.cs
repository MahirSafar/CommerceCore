using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.Catalog.Products;

public sealed class Product : SoftDeletableAggregateRoot<ProductId>
{
    public const int MaximumNameLength = 200;

    public LocalizedText Name { get; private set; } = null!;

    public Money Price { get; private set; } = null!;

    public ProductTypeId ProductTypeId { get; private set; }

    public AttributeValueBag Specifications { get; private set; } = AttributeValueBag.Empty;

    public long ValidatedAgainstVersion { get; private set; }

    public ProductStatus Status { get; private set; }

    private Product()
    {
    }

    private Product(
        ProductId id,
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

        Name = name;
        Price = price;
        ProductTypeId = productTypeId;
        Specifications = AttributeValueBag.Empty;
        ValidatedAgainstVersion = 0;
        Status = ProductStatus.Draft;
    }

    public static Product Create(
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

        Product product = new Product(
            ProductId.New(),
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

        if (Price.Amount == 0)
        {
            throw new ProductDomainException(
                "product.activation_requires_price",
                "A product with a zero price cannot be activated.");
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