using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Enums;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;

namespace CommerceCore.Domain.UnitTests.Catalog.Products;

public class ProductTests
{
    private static readonly DateTimeOffset TestTime = new(2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

    private static LocalizedText CreateValidName(string text = "Test məhsulu") => 
        LocalizedText.Create(LanguageCode.Create("en"), new Dictionary<LanguageCode, string> { { LanguageCode.Create("en"), text } });

    private static Money CreateValidPrice(decimal amount = 100) => Money.Create(amount, "USD");

    private static Product CreateValidProduct() =>
        Product.Create(
            CreateValidName(),
            CreateValidPrice(),
            ProductTypeId.New(),
            TestTime);
    
    [Fact]
    public void Archive_ShouldRaiseProductArchivedDomainEvent()
    {
        Product product = CreateValidProduct();
        DateTimeOffset archivedAtUtc = new DateTimeOffset(
            2026, 8, 13, 12, 0, 0, TimeSpan.Zero);

        bool result = product.Archive(archivedAtUtc, "Admin");

        Assert.True(result);
        IDomainEvent? domainEvent = product.DomainEvents.SingleOrDefault(e => e is ProductArchivedDomainEvent);
        Assert.NotNull(domainEvent);
        ProductArchivedDomainEvent archivedEvent = (ProductArchivedDomainEvent)domainEvent!;
        Assert.Equal(product.Id, archivedEvent.ProductId);
        Assert.Equal("Admin", archivedEvent.ArchivedBy);
        Assert.Equal(archivedAtUtc, archivedEvent.ArchivedAtUtc);
        Assert.Equal(archivedAtUtc, archivedEvent.OccurredOnUtc);
    }

    [Fact]
    public void Archive_IsIdempotent_SecondCallReturnsFalseAndNoNewEvent()
    {
        Product product = CreateValidProduct();
        DateTimeOffset archivedAtUtc = TestTime;

        bool first = product.Archive(archivedAtUtc, "Admin");
        Assert.True(first);

        int beforeCount = product.DomainEvents.Count;

        bool second = product.Archive(archivedAtUtc.AddMinutes(1), "Admin2");
        Assert.False(second);
        Assert.Equal(beforeCount, product.DomainEvents.Count);
    }

    [Fact]
    public void Restore_IsIdempotent_SecondCallReturnsFalse()
    {
        Product product = CreateValidProduct();
        DateTimeOffset archivedAtUtc = TestTime;
        product.Archive(archivedAtUtc, "Admin");

        bool first = product.Restore();
        Assert.True(first);

        bool second = product.Restore();
        Assert.False(second);
    }

    [Fact]
    public void ChangePrice_CurrencyChange_ThrowsProductDomainException()
    {
        Product product = CreateValidProduct();
        Money newPrice = Money.Create(100, "EUR");

        ProductDomainException ex = Assert.Throws<ProductDomainException>(() => product.ChangePrice(newPrice));
        Assert.Equal("product.currency_change_not_allowed", ex.Code);
    }

    [Fact]
    public void ChangeName_SameValue_ReturnsFalse_And_ChangePrice_SameValue_ReturnsFalse()
    {
        Product product = CreateValidProduct();
        LocalizedText sameName = product.Name;
        Money samePrice = product.Price;

        bool nameResult = product.ChangeName(sameName);
        Assert.False(nameResult);

        bool priceResult = product.ChangePrice(samePrice);
        Assert.False(priceResult);
    }

    [Fact]
    public void ActiveProduct_ChangePriceToZero_ThrowsProductDomainException()
    {
        Product product = CreateValidProduct();
        product.Activate();

        ProductDomainException ex = Assert.Throws<ProductDomainException>(() => product.ChangePrice(Money.Create(0m, product.Price.Currency)));
        Assert.Equal("product.active_price_must_be_positive", ex.Code);
    }

    [Fact]
    public void Archive_SetsDeletionFields()
    {
        Product product = CreateValidProduct();
        DateTimeOffset archivedAt = TestTime;

        bool result = product.Archive(archivedAt, "Admin");

        Assert.True(result);
        Assert.True(product.IsDeleted);
        Assert.Equal(archivedAt.ToUniversalTime(), product.DeletedAtUtc);
        Assert.Equal("Admin", product.DeletedBy);
    }

    [Fact]
    public void Archive_WithNonUtcOffset_PreservesUtc()
    {
        Product product = CreateValidProduct();
        DateTimeOffset localTime = new DateTimeOffset(2026, 8, 13, 15, 0, 0, TimeSpan.FromHours(3));

        bool result = product.Archive(localTime, "Admin");

        Assert.True(result);
        DateTimeOffset expectedUtc = localTime.ToUniversalTime();
        Assert.Equal(expectedUtc, product.DeletedAtUtc);

        ProductArchivedDomainEvent domainEvent = product.DomainEvents.OfType<ProductArchivedDomainEvent>().Single();
        Assert.Equal(expectedUtc, domainEvent.ArchivedAtUtc);
        Assert.Equal(expectedUtc, domainEvent.OccurredOnUtc);
    }

    [Fact]
    public void ArchivedProduct_ShouldNotAllowNameOrPriceChanges()
    {
        Product product = CreateValidProduct();
        product.Archive(TestTime, "Admin");
        LocalizedText newName = CreateValidName("Yeni Ad");
        Money newPrice = CreateValidPrice(200);

        ProductDomainException nameException = Assert.Throws<ProductDomainException>(() => product.ChangeName(newName));
        Assert.Equal("product.archived", nameException.Code);

        ProductDomainException priceException = Assert.Throws<ProductDomainException>(() => product.ChangePrice(newPrice));
        Assert.Equal("product.archived", priceException.Code);
    }

    [Fact]
    public void Activate_WithZeroPrice_ShouldThrowProductDomainException()
    {
        Product product = Product.Create(
            CreateValidName(),
            CreateValidPrice(0),
            ProductTypeId.New(),
            TestTime);

        ProductDomainException exception = Assert.Throws<ProductDomainException>(() => product.Activate());
        Assert.Equal("product.activation_requires_price", exception.Code);
    }

    [Fact]
    public void Create_ShouldRaiseProductCreatedDomainEvent()
    {
        Product product = CreateValidProduct();

        IDomainEvent domainEvent = Assert.Single(product.DomainEvents);

        ProductCreatedDomainEvent createdEvent = Assert.IsType<ProductCreatedDomainEvent>(
            domainEvent);

        Assert.Equal(product.Id, createdEvent.ProductId);
        Assert.Equal(TestTime, createdEvent.OccurredOnUtc);
    }

    [Fact]
    public void Restore_ShouldSetStatusToInactive_IfProductWasActiveBeforeArchiving()
    {
        Product product = CreateValidProduct();
        product.Activate();
        product.Archive(TestTime, "Admin");

        product.Restore();

        Assert.Equal(ProductStatus.Inactive, product.Status);
    }

    [Fact]
    public void ClearDomainEvents_ShouldEmptyDomainEventsCollection()
    {
        Product product = CreateValidProduct();
        product.Archive(TestTime, "System");

        Assert.NotEmpty(product.DomainEvents);

        ((IHasDomainEvents)product).ClearDomainEvents();

        Assert.Empty(product.DomainEvents);
    }

    [Fact]
    public void Activate_WithPositivePrice_SetsStatusToActive()
    {
        Product product = CreateValidProduct();

        bool result = product.Activate();

        Assert.True(result);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void Activate_WhenAlreadyActive_ReturnsFalse()
    {
        Product product = CreateValidProduct();

        product.Activate();

        bool result = product.Activate();

        Assert.False(result);
        Assert.Equal(ProductStatus.Active, product.Status);
    }

    [Fact]
    public void Deactivate_WhenProductIsActive_SetsStatusToInactive()
    {
        Product product = CreateValidProduct();
        product.Activate();

        bool result = product.Deactivate();

        Assert.True(result);
        Assert.Equal(ProductStatus.Inactive, product.Status);
    }

    [Fact]
    public void Deactivate_WhenProductIsDraft_ReturnsFalse()
    {
        Product product = CreateValidProduct();

        bool result = product.Deactivate();

        Assert.False(result);
        Assert.Equal(ProductStatus.Draft, product.Status);
    }

    [Fact]
    public void ChangeName_WithDifferentLocalizedText_ChangesName()
    {
        Product product = CreateValidProduct();

        LocalizedText newName = LocalizedText.Create(
            LanguageCode.Create("az"),
            [
                new KeyValuePair<LanguageCode, string>(
                LanguageCode.Create("az"),
                "Yeni məhsul adı"),
            new KeyValuePair<LanguageCode, string>(
                LanguageCode.Create("en"),
                "New product name")
            ]);

        bool changed = product.ChangeName(newName);

        Assert.True(changed);
        Assert.Equal("az", product.Name.DefaultLanguage.Value);
        Assert.Equal("Yeni məhsul adı", product.Name.Get(LanguageCode.Create("az")));
        Assert.Equal("New product name", product.Name.Get(LanguageCode.Create("en")));
    }
}
