using Mediator;

namespace CommerceCore.Application.Catalog.ProductTypes.Queries.GetProductTypeSchema;

public sealed record GetProductTypeSchemaQuery(Guid ProductTypeId)
    : IQuery<ProductTypeSchemaResult?>;

public sealed record ProductTypeSchemaResult(
    Guid ProductTypeId,
    long EffectiveSchemaVersion,
    IReadOnlyList<ProductTypeAttributeSchema> Attributes);

public sealed record ProductTypeAttributeSchema(
    string Key,
    string DataType,
    string Scope,
    bool IsRequired,
    string EnforcementStatus,
    bool IsDeprecated,
    int? MinimumLength,
    int? MaximumLength,
    decimal? MinimumValue,
    decimal? MaximumValue,
    string? MeasurementUnitFamily,
    IReadOnlyList<ProductTypeAttributeOptionSchema> Options);

public sealed record ProductTypeAttributeOptionSchema(
    string Code,
    bool IsDeprecated);
