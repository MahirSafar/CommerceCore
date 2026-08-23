using CommerceCore.Domain.Catalog.Products.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.Products.ValueObjects;

public sealed class VariantSkuTests
{
    [Theory]
    [InlineData(" laptop-16-black ", "LAPTOP-16-BLACK")]
    [InlineData("sku_001", "SKU_001")]
    [InlineData("abc.123", "ABC.123")]
    public void Create_NormalizesSku(
        string input,
        string expected) =>
        Assert.Equal(expected, VariantSku.Create(input).Value);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("sku 001")]
    [InlineData("sku/001")]
    [InlineData("sku@001")]
    [InlineData("məhsul-001")]
    public void Create_WithInvalidCharacters_ThrowsArgumentException(
        string value) =>
        Assert.Throws<ArgumentException>(
            () => VariantSku.Create(value));

    [Fact]
    public void Create_WhenSkuExceedsMaximumLength_ThrowsArgumentOutOfRangeException() =>
        Assert.Throws<ArgumentOutOfRangeException>(
            () => VariantSku.Create(new('A', VariantSku.MaximumLength + 1)));
}