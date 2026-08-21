using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.Catalog.Attributes.Services;

public static class MeasurementUnitNormalizer
{
    public static bool TryNormalize(
        MeasurementUnitFamily family,
        decimal value,
        string? unit,
        out AttributeValue.Measurement? measurement)
    {
        measurement = null;

        if (string.IsNullOrWhiteSpace(family.Value) ||
            string.IsNullOrWhiteSpace(unit))
        {
            return false;
        }

        string normalizedUnit = unit.Trim().ToLowerInvariant();

        if (!TryGetConversion(
                family.Value,
                normalizedUnit,
                out decimal multiplier,
                out string canonicalUnit))
        {
            return false;
        }

        try
        {
            measurement = AttributeValue.Measurement.Create(
                value,
                normalizedUnit,
                checked(value * multiplier),
                canonicalUnit);

            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    private static bool TryGetConversion(
        string family,
        string unit,
        out decimal multiplier,
        out string canonicalUnit)
    {
        (multiplier, canonicalUnit) = (0m, string.Empty);

        return family switch
        {
            "mass" => TryGetMassConversion(
                unit,
                out multiplier,
                out canonicalUnit),

            "length" => TryGetLengthConversion(
                unit,
                out multiplier,
                out canonicalUnit),

            "volume" => TryGetVolumeConversion(
                unit,
                out multiplier,
                out canonicalUnit),

            _ => false
        };
    }

    private static bool TryGetMassConversion(
        string unit,
        out decimal multiplier,
        out string canonicalUnit)
    {
        canonicalUnit = "g";

        multiplier = unit switch
        {
            "mg" => 0.001m,
            "g" => 1m,
            "kg" => 1_000m,
            "t" => 1_000_000m,
            "oz" => 28.349523125m,
            "lb" => 453.59237m,
            _ => 0m
        };

        return multiplier != 0m;
    }

    private static bool TryGetLengthConversion(
        string unit,
        out decimal multiplier,
        out string canonicalUnit)
    {
        canonicalUnit = "mm";

        multiplier = unit switch
        {
            "mm" => 1m,
            "cm" => 10m,
            "m" => 1_000m,
            "km" => 1_000_000m,
            "in" => 25.4m,
            "ft" => 304.8m,
            _ => 0m
        };

        return multiplier != 0m;
    }

    private static bool TryGetVolumeConversion(
        string unit,
        out decimal multiplier,
        out string canonicalUnit)
    {
        canonicalUnit = "ml";

        multiplier = unit switch
        {
            "ml" => 1m,
            "cl" => 10m,
            "dl" => 100m,
            "l" => 1_000m,
            "m3" => 1_000_000m,
            _ => 0m
        };

        return multiplier != 0m;
    }
}