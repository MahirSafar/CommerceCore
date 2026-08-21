using CommerceCore.Domain.Catalog.Attributes.Services;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.UnitTests.Catalog.Attributes.Services;

public sealed class MeasurementUnitNormalizerTests
{
    [Theory]
    [InlineData("mass", 1.5, "kg", 1500, "g")]
    [InlineData("mass", 2, "lb", 907.18474, "g")]
    [InlineData("length", 15.6, "in", 396.24, "mm")]
    [InlineData("volume", 0.75, "l", 750, "ml")]
    public void TryNormalize_WithSupportedUnit_ReturnsCanonicalMeasurement(
        string family,
        decimal value,
        string unit,
        decimal expectedCanonicalValue,
        string expectedCanonicalUnit)
    {
        bool success = MeasurementUnitNormalizer.TryNormalize(
            MeasurementUnitFamily.Create(family),
            value,
            unit,
            out AttributeValue.Measurement? result);

        Assert.True(success);
        Assert.NotNull(result);

        Assert.Equal(value, result.Value);
        Assert.Equal(unit, result.Unit);
        Assert.Equal(expectedCanonicalValue, result.CanonicalValue);
        Assert.Equal(expectedCanonicalUnit, result.CanonicalUnit);
    }

    [Theory]
    [InlineData("mass", "cm")]
    [InlineData("length", "kg")]
    [InlineData("volume", "g")]
    [InlineData("temperature", "c")]
    public void TryNormalize_WithUnsupportedFamilyOrUnit_ReturnsFalse(
        string family,
        string unit)
    {
        bool success = MeasurementUnitNormalizer.TryNormalize(
            MeasurementUnitFamily.Create(family),
            10m,
            unit,
            out AttributeValue.Measurement? result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void TryNormalize_NormalizesUnitCasingAndWhitespace()
    {
        bool success = MeasurementUnitNormalizer.TryNormalize(
            MeasurementUnitFamily.Create("mass"),
            1m,
            " KG ",
            out AttributeValue.Measurement? result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal("kg", result.Unit);
        Assert.Equal(1_000m, result.CanonicalValue);
        Assert.Equal("g", result.CanonicalUnit);
    }
}