using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using Mediator;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;

public sealed record DefineAttributeCommand(
    Guid ProductTypeId,
    string Key,
    AttributeDataType DataType,
    AttributeScope Scope,
    bool IsRequired,
    int DisplayOrder,
    decimal? MinimumValue,
    decimal? MaximumValue,
    int? MinimumLength,
    int? MaximumLength,
    string? MeasurementUnitFamily)
    : ICommand<DefineAttributeResult>;

public sealed record DefineAttributeResult(Guid AttributeDefinitionId);