using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductSpecifications;

public sealed class SetProductSpecificationsCommandValidator
    : AbstractValidator<SetProductSpecificationsCommand>
{
    public SetProductSpecificationsCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEqual(Guid.Empty)
            .WithMessage("Product ID cannot be empty.");

        RuleFor(command => command.Specifications)
            .NotNull()
            .Must(specifications => specifications.Count <= 50)
            .WithMessage("A product can contain at most 50 specification attributes.");
    }
}