using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Common;
using CommerceCore.Domain.Common.Entities;

namespace CommerceCore.Domain.Catalog.Products;

public sealed class Product : SoftDeletableAggregateRoot<ProductId>
{
    public ProductName Name { get; private set; }
    public Money Price { get; private set; }
    public ProductStatus Status { get; private set; }
    private Product()
    {
        Name = null!;
        Price = null!;
    }
    private Product(ProductId id, ProductName name, Money price) : base(id)
    {
        Name = name;
        Price = price;
        Status = ProductStatus.Draft; 
    }

    public static Product Create(ProductName name, Money price)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(price);

        return new Product(ProductId.New(), name, price);
    }

    public void ChangeName(ProductName newName)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(newName);

        Name = newName;
    }

    public void ChangePrice(Money newPrice)
    {
        EnsureNotArchived();
        ArgumentNullException.ThrowIfNull(newPrice);

        if (Status == ProductStatus.Active && newPrice.Amount == 0)
            throw new ProductDomainException("The price of an active product cannot be zero.");

        Price = newPrice;
    }

    public void Activate()
    {
        EnsureNotArchived();
        if (Price.Amount == 0)
            throw new ProductDomainException("A product with a price of 0 cannot be activated.");
        Status = ProductStatus.Active;
    }

    public void Deactivate()
    {
        EnsureNotArchived();
        Status = ProductStatus.Inactive;
    }

    public void Archive(string? archivedBy = null)
    {
        var now = DateTimeOffset.UtcNow;

        var marked = MarkAsDeletedCore(now, archivedBy);

        if (marked)
            RaiseDomainEvent(new ProductArchivedDomainEvent(Id, now));
    }

    public void Restore()
    {
        RestoreCore();
    }
    private void EnsureNotArchived()
    {
        if (IsDeleted)
            throw new ProductDomainException("No changes can be made to an archived product.");
    }
}
