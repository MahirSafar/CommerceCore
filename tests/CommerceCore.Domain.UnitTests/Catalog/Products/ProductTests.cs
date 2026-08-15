using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Catalog.Products;

public class ProductTests
{
    private static readonly DateTimeOffset TestTime = new(
        2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

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
        return Product.Create(CreateValidName(), CreateValidPrice(), TestTime);
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
        var archivedAtUtc = TestTime;

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
        var archivedAtUtc = TestTime;
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
    public void ActiveProduct_ChangePriceToZero_ThrowsProductDomainException()
    {
        var product = CreateValidProduct();
        product.Activate();

        var ex = Assert.Throws<ProductDomainException>(() => product.ChangePrice(Money.Create(0m, product.Price.Currency)));
        Assert.Equal("product.active_price_must_be_positive", ex.Code);
    }

    [Fact]
    public void Archive_SetsDeletionFields()
    {
        var product = CreateValidProduct();
        var archivedAt = TestTime;

        var result = product.Archive(archivedAt, "Admin");

        Assert.True(result);
        Assert.True(product.IsDeleted);
        Assert.Equal(archivedAt.ToUniversalTime(), product.DeletedAtUtc);
        Assert.Equal("Admin", product.DeletedBy);
    }

    [Fact]
    public void Archive_WithNonUtcOffset_PreservesUtc()
    {
        var product = CreateValidProduct();
        var localTime = new DateTimeOffset(2026, 8, 13, 15, 0, 0, TimeSpan.FromHours(3));

        var result = product.Archive(localTime, "Admin");

        Assert.True(result);
        var expectedUtc = localTime.ToUniversalTime();
        Assert.Equal(expectedUtc, product.DeletedAtUtc);

        var domainEvent = product.DomainEvents.OfType<ProductArchivedDomainEvent>().Single();
        Assert.Equal(expectedUtc, domainEvent.ArchivedAtUtc);
        Assert.Equal(expectedUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void ArchivedProduct_ShouldNotAllowNameOrPriceChanges()
    {
        var product = CreateValidProduct();
        product.Archive(TestTime, "Admin");
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
        var product = Product.Create(CreateValidName(), CreateValidPrice(0), TestTime);

        var exception = Assert.Throws<ProductDomainException>(() => product.Activate());
        Assert.Equal("product.activation_requires_price", exception.Code);
    }

    [Fact]
    public void Create_ShouldRaiseProductCreatedDomainEvent()
    {
        var product = CreateValidProduct();

        var domainEvent = Assert.Single(product.DomainEvents);

        var createdEvent = Assert.IsType<ProductCreatedDomainEvent>(
            domainEvent);

        Assert.Equal(product.Id, createdEvent.ProductId);
        Assert.Equal(TestTime, createdEvent.OccurredOnUtc);
    }

    [Fact]
    public void Restore_ShouldSetStatusToInactive_IfProductWasActiveBeforeArchiving()
    {
        var product = CreateValidProduct();
        product.Activate();
        product.Archive(TestTime, "Admin");

        product.Restore();

        Assert.Equal(ProductStatus.Inactive, product.Status);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyDomainEventsCollection()
    {
        var product = CreateValidProduct();
        product.Archive(TestTime, "System");

        Assert.NotEmpty(product.DomainEvents);

        ((IHasDomainEvents)product).ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }
}
