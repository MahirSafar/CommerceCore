using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.ProductTypes.ValueObjects;

public sealed class AttributeOptionCodeTests
{
    [Theory]
    [InlineData("space-black")]
    [InlineData("medium")]
    [InlineData("250g")]
    [InlineData("caffeine-free")]
    public void Create_WithValidKebabCaseCode_ReturnsCode(string value)
    {
        AttributeOptionCode code = AttributeOptionCode.Create(value);

        Assert.Equal(value, code.Value);
        Assert.Equal(value, code.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Space-Black")]
    [InlineData("space_black")]
    [InlineData("-black")]
    [InlineData("black-")]
    [InlineData("space black")]
    public void Create_WithInvalidCode_ThrowsArgumentException(string value) =>
        Assert.Throws<ArgumentException>(() => AttributeOptionCode.Create(value));

    [Fact]
    public void Value_OnDefaultInstance_ThrowsInvalidOperationException() =>
        Assert.Throws<InvalidOperationException>(() => _ = ((AttributeOptionCode)default).Value);
}