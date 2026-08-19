using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Application.Common.Abstractions.Persistence;

public interface IAttributeDefinitionRegistry
{
    Task EnsureKeyIsUniqueInHierarchyAsync(
        ProductTypeId productTypeId,
        AttributeKey attributeKey,
        CancellationToken cancellationToken = default);
}
