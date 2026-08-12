using CommerceCore.Domain.Catalog.Products;
using System;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Domain.Common.Localization;
using CommerceCore.Domain.Catalog.Products.Enums;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace CommerceCore.Domain.Tests.Catalog.Products;

public class ProductTests
{
    private static LocalizedText CreateValidName(string text = "Test məhsulu")
    {
        var lang = LanguageCode.Create("en");
        return LocalizedText.Create(lang, new Dictionary<LanguageCode, string> { { lang, text } });
    }

    private static Money CreateValidPrice(decimal amount = 100)
    {
        return Money.Create(amount, "USD");
    }

    private static Product CreateValidProduct()
    {
        return Product.Create(CreateValidName(), CreateValidPrice());
    }

    [Fact]
    public void Archive_ShouldRaiseProductArchivedDomainEvent()
    {
        var product = CreateValidProduct();
        var archivedAtUtc = new DateTimeOffset(
            2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        var result = product.Archive(archivedAtUtc, "Admin");

        Assert.True(result);
        var domainEvent = product.DomainEvents.SingleOrDefault(e => e is ProductArchivedDomainEvent);
        Assert.NotNull(domainEvent);
        var archivedEvent = (ProductArchivedDomainEvent)domainEvent!;
        Assert.Equal(product.Id, archivedEvent.ProductId);
        Assert.Equal("Admin", archivedEvent.ArchivedBy);
        Assert.Equal(archivedAtUtc, archivedEvent.ArchivedAtUtc);
        Assert.Equal(archivedAtUtc, archivedEvent.OccurredOnUtc);
    }

    [Fact]
    public void Archive_IsIdempotent_SecondCallReturnsFalseAndNoNewEvent()
    {
        var product = CreateValidProduct();
        var archivedAtUtc = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        var first = product.Archive(archivedAtUtc, "Admin");
        Assert.True(first);

        var beforeCount = product.DomainEvents.Count;

        var second = product.Archive(archivedAtUtc.AddMinutes(1), "Admin2");
        Assert.False(second);
        Assert.Equal(beforeCount, product.DomainEvents.Count);
    }

    [Fact]
    public void Restore_IsIdempotent_SecondCallReturnsFalse()
    {
        var product = CreateValidProduct();
        var archivedAtUtc = new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);
        product.Archive(archivedAtUtc, "Admin");

        var first = product.Restore();
        Assert.True(first);

        var second = product.Restore();
        Assert.False(second);
    }

    [Fact]
    public void ChangePrice_CurrencyChange_ThrowsProductDomainException()
    {
        var product = CreateValidProduct();
        var newPrice = Money.Create(100, "EUR");

        var ex = Assert.Throws<ProductDomainException>(() => product.ChangePrice(newPrice));
        Assert.Equal("product.currency_change_not_allowed", ex.Code);
    }

    [Fact]
    public void ChangeName_SameValue_ReturnsFalse_And_ChangePrice_SameValue_ReturnsFalse()
    {
        var product = CreateValidProduct();
        var sameName = product.Name;
        var samePrice = product.Price;

        var nameResult = product.ChangeName(sameName);
        Assert.False(nameResult);

        var priceResult = product.ChangePrice(samePrice);
        Assert.False(priceResult);
    }

    [Fact]
    public void ArchivedProduct_ShouldNotAllowNameOrPriceChanges()
    {
        var product = CreateValidProduct();
        product.Archive(DateTimeOffset.UtcNow, "Admin");
        var newName = CreateValidName("Yeni Ad");
        var newPrice = CreateValidPrice(200);

        var nameException = Assert.Throws<ProductDomainException>(() => product.ChangeName(newName));
        Assert.Equal("product.archived", nameException.Code);

        var priceException = Assert.Throws<ProductDomainException>(() => product.ChangePrice(newPrice));
        Assert.Equal("product.archived", priceException.Code);
    }

    [Fact]
    public void Activate_WithZeroPrice_ShouldThrowProductDomainException()
    {
        var product = Product.Create(CreateValidName(), CreateValidPrice(0));

        var exception = Assert.Throws<ProductDomainException>(() => product.Activate());
        Assert.Equal("product.activation_requires_price", exception.Code);
    }

    [Fact]
    public void Restore_ShouldSetStatusToInactive_IfProductWasActiveBeforeArchiving()
    {
        var product = CreateValidProduct();
        product.Activate();
        product.Archive(DateTimeOffset.UtcNow, "Admin");

        product.Restore();

        Assert.Equal(ProductStatus.Inactive, product.Status);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyDomainEventsCollection()
    {
        var product = CreateValidProduct();
        product.Archive(DateTimeOffset.UtcNow, "System");

        Assert.NotEmpty(product.DomainEvents);

        ((IHasDomainEvents)product).ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }
}

public class BaseEntityEqualityTests
{
    private sealed class TestEntity(Guid id) : BaseEntity<Guid>(id)
    {
    }

    [Fact]
    public void Entities_WithSameId_ShouldBeEqual()
    {
        var id = Guid.NewGuid();
        var entity1 = new TestEntity(id);
        var entity2 = new TestEntity(id);

        Assert.True(entity1.Equals(entity2));
        Assert.True(entity1 == entity2);
        Assert.Equal(entity1.GetHashCode(), entity2.GetHashCode());
    }
}
