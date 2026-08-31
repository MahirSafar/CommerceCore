using CommerceCore.Domain.Common.ValueObjects;
using FluentValidation;

namespace CommerceCore.Application.Common.Validation;

public static class MoneyValidationRules
{
    public static IRuleBuilderOptions<T, decimal> HasValidMoneyAmount<T>(this IRuleBuilder<T, decimal> ruleBuilder) =>
        ruleBuilder
            .GreaterThanOrEqualTo(0m)
            .Must(HasValidScale)
            .WithMessage(
                $"Price cannot have more than {Money.MaximumScale} decimal places.");


    public static IRuleBuilderOptions<T, string> HasValidCurrency<T>(this IRuleBuilder<T, string> ruleBuilder) =>
        ruleBuilder
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");

    private static bool HasValidScale(decimal amount)
        => ((decimal.GetBits(amount)[3] >> 16) & 0xFF) <= Money.MaximumScale;
}