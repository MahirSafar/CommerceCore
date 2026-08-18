using Mediator;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;

public sealed record CreateProductTypeCommand(
    string Code,
    Guid? ParentProductTypeId,
    bool IsAssignable)
    : ICommand<CreateProductTypeResult>;

public sealed record CreateProductTypeResult(Guid ProductTypeId);