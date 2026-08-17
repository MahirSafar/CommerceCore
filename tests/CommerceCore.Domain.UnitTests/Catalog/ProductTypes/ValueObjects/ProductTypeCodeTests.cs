using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes.ValueObjects;

public sealed class ProductTypeCodeTests
{
    [Theory]
    [InlineData("electronics")]
    [InlineData("gaming_laptop")]
    [InlineData("coffee_beans")]
    public void Create_WithValidCode_ReturnsNormalizedCode(string value)
    {
        ProductTypeCode code = ProductTypeCode.Create(value);

        Assert.Equal(value, code.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("GamingLaptop")]
    [InlineData("gaming-laptop")]
    [InlineData("_gaming")]
    [InlineData("gaming_")]
    [InlineData("gaming laptop")]
    public void Create_WithInvalidCode_ThrowsArgumentException(string value) =>
        Assert.Throws<ArgumentException>(() => ProductTypeCode.Create(value));
}