using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.Entities;

namespace CommerceCore.Domain.Catalog.ProductTypes;

public sealed class AttributeOption : BaseEntity<AttributeOptionId>
{
    private AttributeOption()
    {
    }

    private AttributeOption(
        AttributeOptionId id,
        AttributeDefinitionId attributeDefinitionId,
        AttributeOptionCode code,
        int displayOrder)
        : base(id)
    {
        if (attributeDefinitionId.Value == Guid.Empty)
            throw new ArgumentException( "Attribute definition ID cannot be empty.", nameof(attributeDefinitionId));

        if (displayOrder < 0)
            throw new ArgumentOutOfRangeException(nameof(displayOrder), "Display order cannot be negative.");

        AttributeDefinitionId = attributeDefinitionId;
        Code = code;
        DisplayOrder = displayOrder;
    }

    public AttributeDefinitionId AttributeDefinitionId { get; private set; }

    public AttributeOptionCode Code { get; private set; }

    public int DisplayOrder { get; private set; }

    public bool IsDeprecated { get; private set; }

    public static AttributeOption Create(AttributeDefinitionId attributeDefinitionId, AttributeOptionCode code, int displayOrder)
        => new(AttributeOptionId.New(), attributeDefinitionId, code, displayOrder);
    public bool Deprecate()
    {
        if (IsDeprecated)
            return false;

        IsDeprecated = true;
        return true;
    }
}