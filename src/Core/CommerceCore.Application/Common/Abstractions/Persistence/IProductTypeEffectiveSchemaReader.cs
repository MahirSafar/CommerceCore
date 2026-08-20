using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Application.Common.Abstractions.Persistence;

public interface IProductTypeEffectiveSchemaReader
{
    Task<EffectiveProductTypeSchema?> GetAsync(
        ProductTypeId productTypeId,
        CancellationToken cancellationToken);
}

public sealed record EffectiveProductTypeSchema(
    long EffectiveSchemaVersion,
    IReadOnlyList<EffectiveAttributeDefinition> Attributes);

public sealed record EffectiveAttributeDefinition(
    AttributeKey Key,
    AttributeDataType DataType,
    AttributeScope Scope,
    bool IsRequired,
    AttributeEnforcementStatus EnforcementStatus,
    bool IsDeprecated,
    int? MinimumLength,
    int? MaximumLength,
    decimal? MinimumValue,
    decimal? MaximumValue,
    MeasurementUnitFamily? MeasurementUnitFamily,
    IReadOnlyList<EffectiveAttributeOption> Options);

public sealed record EffectiveAttributeOption(
    AttributeOptionCode Code,
    bool IsDeprecated);