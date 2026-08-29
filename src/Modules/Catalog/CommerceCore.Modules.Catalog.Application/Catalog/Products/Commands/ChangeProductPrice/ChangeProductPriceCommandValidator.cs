using CommerceCore.Application.Common.Validation;
using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.ChangeProductPrice;

public sealed class ChangeProductPriceCommandValidator : AbstractValidator<ChangeProductPriceCommand>
{
    public ChangeProductPriceCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEmpty();

        RuleFor(command => command.PriceAmount)
            .HasValidMoneyAmount();

        RuleFor(command => command.Currency)
            .HasValidCurrency();
    }
}
