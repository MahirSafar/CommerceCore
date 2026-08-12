using CommerceCore.Domain.Common.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Common.ValueObjects;

public class MoneyTests
{
    [Fact]
    public void Create_NegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(-1m, "USD"));
    }

    [Fact]
    public void Create_InvalidCurrency_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => Money.Create(10m, "US"));
        Assert.Throws<ArgumentException>(() => Money.Create(10m, "123"));
    }

    [Fact]
    public void Create_TooManyDecimalPlaces_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.Create(1.12345m, "USD"));
    }
}
