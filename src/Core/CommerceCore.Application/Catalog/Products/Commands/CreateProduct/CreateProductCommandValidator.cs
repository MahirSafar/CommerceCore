using CommerceCore.Application.Common.Validation;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.CreateProduct;

public sealed class CreateProductCommandValidator : AbstractValidator<CreateProductCommand>
{
    private const string LanguageTagPattern = "^[A-Za-z]{2,3}(?:-[A-Za-z0-9]{2,8})*$";

    private static bool HasValidScale(decimal amount) => ((decimal.GetBits(amount)[3] >> 16) & 0xFF) <= Money.MaximumScale;

    private static void ValidateTranslations(CreateProductCommand command, ValidationContext<CreateProductCommand> context)
    {
        if (command.NameTranslations is null)
            return;

        var languages = new HashSet<LanguageCode>();

        foreach (var translation in command.NameTranslations)
        {
            LanguageCode language;

            try
            {
                language = LanguageCode.Create(translation.Key);
            }
            catch (ArgumentException)
            {
                context.AddFailure(
                    nameof(command.NameTranslations),
                    $"'{translation.Key}' is not a valid language code.");

                continue;
            }

            if (!languages.Add(language))
            {
                context.AddFailure(
                    nameof(command.NameTranslations),
                    $"Duplicate translation exists for '{language}'.");
            }

            if (string.IsNullOrWhiteSpace(translation.Value))
            {
                context.AddFailure(
                    nameof(command.NameTranslations),
                    $"Translation for '{language}' cannot be empty.");
            }
            else if (translation.Value.Trim().Length > Product.MaximumNameLength)
            {
                context.AddFailure(
                    nameof(command.NameTranslations),
                    $"Translation for '{language}' cannot exceed " +
                    $"{Product.MaximumNameLength} characters.");
            }
        }

        try
        {
            var defaultLanguage = LanguageCode.Create(command.DefaultLanguage);

            if (!languages.Contains(defaultLanguage))
            {
                context.AddFailure(
                    nameof(command.NameTranslations),
                    "A translation for the default language is required.");
            }
        }
        catch (ArgumentException)
        {
        }
    }
    public CreateProductCommandValidator()
    {
        RuleFor(command => command.DefaultLanguage)
            .NotEmpty()
            .Matches(LanguageTagPattern);

        RuleFor(command => command.NameTranslations)
            .NotNull()
            .NotEmpty();

        RuleFor(command => command.PriceAmount)
            .GreaterThanOrEqualTo(0m)
            .Must(HasValidScale)
            .WithMessage($"Price cannot have more than {Money.MaximumScale} decimal places.");

        RuleFor(command => command.Currency)
            .NotEmpty()
            .Matches("^[A-Za-z]{3}$");

        RuleFor(command => command)
            .Custom(ValidateTranslations);

        RuleFor(command => command.PriceAmount)
            .HasValidMoneyAmount();

        RuleFor(command => command.Currency)
            .HasValidCurrency();
    }
}
