using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes.ValueObjects;

public sealed class AttributeKeyTests
{
    [Theory]
    [InlineData("cpu")]
    [InlineData("ram_gb")]
    [InlineData("screen_size_inches")]
    [InlineData("usb4_port_count")]
    public void Create_WithValidKey_ReturnsKey(string value)
    {
        AttributeKey key = AttributeKey.Create(value);

        Assert.Equal(value, key.Value);
    }

    [Theory]
    [InlineData("RamGb")]
    [InlineData("_ram_gb")]
    [InlineData("ram_gb_")]
    [InlineData("ram-gb")]
    [InlineData("ram gb")]
    [InlineData(" ram_gb")]
    [InlineData("ram_gb ")]
    public void Create_WithInvalidKey_ThrowsArgumentException(string value)
    {
        Assert.Throws<ArgumentException>(() => AttributeKey.Create(value));
    }

    [Fact]
    public void Create_WithMoreThanMaximumLength_ThrowsArgumentException()
    {
        string value = $"a{new string('x', AttributeKey.MaximumLength)}";

        Assert.Throws<ArgumentException>(() => AttributeKey.Create(value));
    }
}