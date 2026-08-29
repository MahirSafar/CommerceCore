using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Persistence.ProductTypes;

public sealed class ProductTypeEffectiveSchema
{
    private ProductTypeEffectiveSchema()
    {
    }

    public TenantId TenantId { get; private set; }
    public ProductTypeId ProductTypeId { get; private set; }

    public long EffectiveSchemaVersion { get; private set; }
    public string Schema { get; private set; } = null!;

    public DateTimeOffset UpdatedAtUtc { get; private set; }
}