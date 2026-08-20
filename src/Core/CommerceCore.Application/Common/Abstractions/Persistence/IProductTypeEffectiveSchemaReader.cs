using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Application.Common.Abstractions.Persistence;

public interface IProductTypeEffectiveSchemaReader
{
    Task<EffectiveProductTypeSchema?> GetAsync(
        ProductTypeId productTypeId,
        CancellationToken cancellationToken);
}