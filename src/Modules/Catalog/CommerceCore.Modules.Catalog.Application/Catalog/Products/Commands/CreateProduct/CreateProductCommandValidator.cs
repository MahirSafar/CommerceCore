using CommerceCore.Application.Common.Validation;
using CommerceCore.Domain.Catalog.Products;
using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator
    : AbstractValidator<CreateProductCommand>
{
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.DefaultLanguage)
            .NotEmpty()
            .Matches(LocalizedTextValidationRules.LanguageTagPattern);

        RuleFor(command => command.NameTranslations)
            .NotNull()
            .NotEmpty();

        RuleFor(command => command.PriceAmount)
            .HasValidMoneyAmount();

        RuleFor(command => command.Currency)
            .HasValidCurrency();

        RuleFor(command => command)
            .Custom((command, context) =>
                LocalizedTextValidationRules.Validate(
                    command.DefaultLanguage,
                    command.NameTranslations,
                    context,
                    nameof(command.NameTranslations),
                    Product.MaximumNameLength));

        RuleFor(command => command.ProductTypeId)
            .NotEqual(Guid.Empty)
            .WithMessage("Product type ID cannot be empty.");
    }
}