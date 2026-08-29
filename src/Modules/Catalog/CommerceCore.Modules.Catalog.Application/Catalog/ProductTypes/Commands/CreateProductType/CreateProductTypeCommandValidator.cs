using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;

public sealed class CreateProductTypeCommandValidator : AbstractValidator<CreateProductTypeCommand>
{
    public CreateProductTypeCommandValidator()
    {
        RuleFor(command => command.Code)
            .NotEmpty()
            .Must(BeValidProductTypeCode)
            .WithMessage(
                "Product type code must be lowercase snake_case, start with a letter, " +
                "end with a letter or digit, and be at most 64 characters.");

        RuleFor(command => command.ParentProductTypeId)
            .NotEqual(Guid.Empty)
            .When(command => command.ParentProductTypeId.HasValue)
            .WithMessage("Parent product type ID cannot be empty.");
    }

    private static bool BeValidProductTypeCode(string? value)
    {
        try
        {
            _ = ProductTypeCode.Create(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}