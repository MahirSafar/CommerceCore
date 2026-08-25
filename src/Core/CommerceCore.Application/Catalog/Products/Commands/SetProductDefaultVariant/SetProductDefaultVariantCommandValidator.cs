using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.SetProductDefaultVariant;

public sealed class SetProductDefaultVariantCommandValidator
    : AbstractValidator<SetProductDefaultVariantCommand>
{
    public SetProductDefaultVariantCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.ProductVariantId)
            .NotEmpty();
    }
}