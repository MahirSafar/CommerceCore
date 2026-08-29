using CommerceCore.Application.Common.Validation;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.AddProductVariant;

public sealed class AddProductVariantCommandValidator
    : AbstractValidator<AddProductVariantCommand>
{
    public AddProductVariantCommandValidator()
    {
        RuleFor(command => command.ProductId)
            .NotEqual(Guid.Empty)
            .WithMessage("Product ID cannot be empty.");

        RuleFor(command => command.Sku)
            .NotEmpty()
            .MaximumLength(VariantSku.MaximumLength)
            .Must(BeValidSku)
            .WithMessage(
                "SKU can contain only ASCII letters, digits, '-', '_' and '.'.");

        RuleFor(command => command.PriceAmount)
            .HasValidMoneyAmount();

        RuleFor(command => command.Currency)
            .HasValidCurrency();

        RuleFor(command => command.Options)
            .NotNull();
    }

    private static bool BeValidSku(string? sku)
    {
        try
        {
            VariantSku.Create(sku!);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}