using System.Collections.ObjectModel;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.Catalog.Attributes.ValueObjects;

public sealed class AttributeValueBag : IEquatable<AttributeValueBag>
{
    private readonly Dictionary<AttributeKey, AttributeValue> _values;
    private readonly ReadOnlyDictionary<AttributeKey, AttributeValue>
        _readOnlyValues;

    public AttributeValueBag()
        : this([])
    {
    }

    private AttributeValueBag(
        Dictionary<AttributeKey, AttributeValue> values)
    {
        _values = values;
        _readOnlyValues = new ReadOnlyDictionary<
            AttributeKey,
            AttributeValue>(_values);
    }

    public static AttributeValueBag Empty => new();

    public IReadOnlyDictionary<AttributeKey, AttributeValue> Values
        => _readOnlyValues;

    public int Count => _values.Count;

    public bool Contains(AttributeKey key) => _values.ContainsKey(key);

    public bool TryGetValue(
        AttributeKey key,
        out AttributeValue? value)
        => _values.TryGetValue(key, out value);

    public AttributeValueBag With(
        AttributeKey key,
        AttributeValue value)
    {
        EnsureInitialized(key);
        ArgumentNullException.ThrowIfNull(value);

        if (_values.TryGetValue(key, out AttributeValue? currentValue) && Equals(currentValue, value))
            return this;

        Dictionary<AttributeKey, AttributeValue> copy = new(_values)
        {
            [key] = value
        };

        return new AttributeValueBag(copy);
    }

    public AttributeValueBag Without(AttributeKey key)
    {
        EnsureInitialized(key);

        if (!_values.ContainsKey(key))
            return this;

        Dictionary<AttributeKey, AttributeValue> copy = new(_values);
        copy.Remove(key);

        return new AttributeValueBag(copy);
    }

    public bool Equals(AttributeValueBag? other)
    {
        if (other is null || Count != other.Count)
            return false;

        foreach ((AttributeKey key, AttributeValue value) in _values)
            if (!other._values.TryGetValue(key, out AttributeValue? otherValue) || !Equals(value, otherValue))
                return false;

        return true;
    }

    public override bool Equals(object? obj)
        => obj is AttributeValueBag other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();

        foreach ((AttributeKey key, AttributeValue value) in _values.OrderBy(pair => pair.Key.Value, StringComparer.Ordinal))
        {
            hash.Add(key);
            hash.Add(value);
        }

        return hash.ToHashCode();
    }

    private static void EnsureInitialized(AttributeKey key) 
    {
        if (key == default)
            throw new ArgumentException("Attribute key must be initialized.", nameof(key));
    }
}