using CommerceCore.Application.Common.Validation;
using CommerceCore.Domain.Catalog.Products;
using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductName;

public sealed class ChangeProductNameCommandValidator
    : AbstractValidator<ChangeProductNameCommand>
{
    public ChangeProductNameCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.DefaultLanguage)
            .NotEmpty()
            .Matches(LocalizedTextValidationRules.LanguageTagPattern);

        RuleFor(command => command.NameTranslations)
            .NotNull()
            .NotEmpty();

        RuleFor(command => command)
            .Custom((command, context) =>
                LocalizedTextValidationRules.Validate(
                    command.DefaultLanguage,
                    command.NameTranslations,
                    context,
                    nameof(command.NameTranslations),
                    Product.MaximumNameLength));
    }
}