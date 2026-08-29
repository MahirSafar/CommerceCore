using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProductVariant;

public sealed class DeactivateProductVariantCommandValidator
    : AbstractValidator<DeactivateProductVariantCommand>
{
    public DeactivateProductVariantCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.ProductVariantId)
            .NotEmpty();
    }
}