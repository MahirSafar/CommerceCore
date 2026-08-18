using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Persistence.ProductTypes;

public sealed class ProductTypeEffectiveSchema
{
    private ProductTypeEffectiveSchema()
    {
    }

    public ProductTypeId ProductTypeId { get; private set; }

    public long SchemaVersion { get; private set; }

    public string Schema { get; private set; } = null!;

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}