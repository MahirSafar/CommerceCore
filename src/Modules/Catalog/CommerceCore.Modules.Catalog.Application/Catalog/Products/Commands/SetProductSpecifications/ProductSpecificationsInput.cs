using System.Collections.ObjectModel;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;

public sealed class ProductSpecificationsInput
{
    private readonly ReadOnlyDictionary<
        AttributeKey,
        ProductSpecificationInputValue> _values;

    public ProductSpecificationsInput(
        IReadOnlyDictionary<
            AttributeKey,
            ProductSpecificationInputValue> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Dictionary<AttributeKey, ProductSpecificationInputValue> copy = [];

        foreach ((AttributeKey key, ProductSpecificationInputValue value)
                 in values)
        {
            if (key == default)
            {
                throw new ArgumentException(
                    "Specification key must be initialized.",
                    nameof(values));
            }

            ArgumentNullException.ThrowIfNull(value);

            copy.Add(key, value);
        }

        _values = new ReadOnlyDictionary<
            AttributeKey,
            ProductSpecificationInputValue>(copy);
    }

    public IReadOnlyDictionary<
        AttributeKey,
        ProductSpecificationInputValue> Values => _values;

    public int Count => _values.Count;

    public static ProductSpecificationsInput FromTypedBag(
        AttributeValueBag specifications)
    {
        ArgumentNullException.ThrowIfNull(specifications);

        Dictionary<AttributeKey, ProductSpecificationInputValue> values = [];

        foreach ((AttributeKey key, AttributeValue value)
                 in specifications.Values)
        {
            values.Add(
                key,
                new ProductSpecificationInputValue.Typed(value));
        }

        return new ProductSpecificationsInput(values);
    }

    public AttributeValueBag ToTypedBag()
    {
        AttributeValueBag result = AttributeValueBag.Empty;

        foreach ((AttributeKey key, ProductSpecificationInputValue value)
                 in Values)
        {
            if (value is not ProductSpecificationInputValue.Typed typed)
            {
                throw new InvalidOperationException(
                    $"Specification '{key.Value}' requires measurement normalization.");
            }

            result = result.With(key, typed.Value);
        }

        return result;
    }
}

public abstract record ProductSpecificationInputValue
{
    public sealed record Typed(AttributeValue Value)
        : ProductSpecificationInputValue;

    public sealed record Measurement(decimal Value, string Unit)
        : ProductSpecificationInputValue;
}