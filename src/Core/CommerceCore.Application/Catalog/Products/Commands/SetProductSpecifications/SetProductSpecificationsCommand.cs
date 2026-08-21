using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using Mediator;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;

public sealed record SetProductSpecificationsCommand(
    Guid ProductId,
    AttributeValueBag Specifications)
    : ICommand<SetProductSpecificationsResult>;

public sealed record SetProductSpecificationsResult(
    Guid ProductId,
    long ValidatedAgainstVersion,
    bool Changed);