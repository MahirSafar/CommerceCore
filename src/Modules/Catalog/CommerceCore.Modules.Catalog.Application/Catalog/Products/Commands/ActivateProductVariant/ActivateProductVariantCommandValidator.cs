using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProductVariant;

public sealed class ActivateProductVariantCommandValidator
    : AbstractValidator<ActivateProductVariantCommand>
{
    public ActivateProductVariantCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.ProductVariantId)
            .NotEmpty();
    }
}