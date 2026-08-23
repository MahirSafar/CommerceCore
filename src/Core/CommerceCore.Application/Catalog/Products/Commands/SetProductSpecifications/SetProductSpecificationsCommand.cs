using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;

public sealed record SetProductSpecificationsCommand(
    Guid ProductId,
    ProductSpecificationsInput Specifications)
    : ICommand<SetProductSpecificationsResult>
{
    public SetProductSpecificationsCommand(
        Guid productId,
        AttributeValueBag specifications)
        : this(
            productId,
            ProductSpecificationsInput.FromTypedBag(specifications))
    {
    }
}

public sealed record SetProductSpecificationsResult(
    Guid ProductId,
    long ValidatedAgainstVersion,
    bool Changed);