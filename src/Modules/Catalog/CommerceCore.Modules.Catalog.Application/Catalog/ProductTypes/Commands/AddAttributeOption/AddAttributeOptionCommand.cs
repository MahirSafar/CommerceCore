using Mediator;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.AddAttributeOption;

public sealed record AddAttributeOptionCommand(
    Guid ProductTypeId,
    Guid AttributeDefinitionId,
    string Code,
    int DisplayOrder)
    : ICommand<AddAttributeOptionResult>;

public sealed record AddAttributeOptionResult(Guid AttributeOptionId);