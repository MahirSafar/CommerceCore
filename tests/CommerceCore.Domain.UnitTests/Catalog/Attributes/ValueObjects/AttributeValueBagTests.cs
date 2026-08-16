using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.Attributes.ValueObjects;

public sealed class AttributeValueBagTests
{
    [Fact]
    public void With_DoesNotMutateOriginalBag()
    {
        AttributeKey key = AttributeKey.Create("ram_gb");

        AttributeValueBag original = AttributeValueBag.Empty;

        AttributeValueBag updated = original.With(
            key,
            AttributeValue.Integer.Create(32));

        Assert.Empty(original.Values);
        Assert.Single(updated.Values);
        Assert.IsType<AttributeValue.Integer>(updated.Values[key]);
    }

    [Fact]
    public void Bags_WithSameValuesInDifferentInsertionOrder_AreEqual()
    {
        AttributeKey cpu = AttributeKey.Create("cpu");
        AttributeKey ram = AttributeKey.Create("ram_gb");

        AttributeValueBag first = AttributeValueBag.Empty
            .With(cpu, AttributeValue.Text.Create("Apple M4"))
            .With(ram, AttributeValue.Integer.Create(32));

        AttributeValueBag second = AttributeValueBag.Empty
            .With(ram, AttributeValue.Integer.Create(32))
            .With(cpu, AttributeValue.Text.Create("Apple M4"));

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Without_WhenKeyExists_ReturnsNewBagWithoutKey()
    {
        AttributeKey key = AttributeKey.Create("ram_gb");

        AttributeValueBag original = AttributeValueBag.Empty.With(
            key,
            AttributeValue.Integer.Create(32));

        AttributeValueBag updated = original.Without(key);

        Assert.True(original.Contains(key));
        Assert.False(updated.Contains(key));
    }

    [Fact]
    public void MultiSelect_Create_NormalizesSortsAndRemovesDuplicates()
    {
        AttributeValue.MultiSelect value =
            AttributeValue.MultiSelect.Create(
                ["red", "blue", "red"]);

        Assert.Equal(["blue", "red"], value.OptionCodes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void MultiSelect_Create_WithInvalidOptionCode_Throws(
        string optionCode)
    {
        Assert.Throws<ArgumentException>(() =>
            AttributeValue.MultiSelect.Create([optionCode]));
    }
}