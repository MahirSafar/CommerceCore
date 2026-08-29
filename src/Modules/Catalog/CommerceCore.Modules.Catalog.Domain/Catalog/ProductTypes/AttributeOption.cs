using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Domain.Catalog.ProductTypes;

public sealed class AttributeOption : BaseEntity<AttributeOptionId>
{
    public TenantId TenantId { get; private set; }

    private AttributeOption()
    {
    }

    private AttributeOption(
        AttributeOptionId id,
        TenantId tenantId,
        AttributeDefinitionId attributeDefinitionId,
        AttributeOptionCode code,
        int displayOrder)
        : base(id)
    {
        if (attributeDefinitionId.Value == Guid.Empty)
            throw new ArgumentException("Attribute definition ID cannot be empty.", nameof(attributeDefinitionId));

        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        if (tenantId.Value == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID cannot be empty.",
                nameof(tenantId));
        }

        TenantId = tenantId;
        AttributeDefinitionId = attributeDefinitionId;
        Code = code;
        DisplayOrder = displayOrder;
    }

    public AttributeDefinitionId AttributeDefinitionId { get; private set; }

    public AttributeOptionCode Code { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsDeprecated { get; private set; }

    public static AttributeOption Create(TenantId tenantId, AttributeDefinitionId attributeDefinitionId, AttributeOptionCode code, int displayOrder)
        => new(AttributeOptionId.New(), tenantId, attributeDefinitionId, code, displayOrder);

    public bool Deprecate()
    {
        if (IsDeprecated)
            return false;

        IsDeprecated = true;
        return true;
    }
}