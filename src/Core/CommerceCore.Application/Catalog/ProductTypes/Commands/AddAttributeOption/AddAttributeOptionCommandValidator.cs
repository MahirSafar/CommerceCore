using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.AddAttributeOption;

public sealed class AddAttributeOptionCommandValidator
    : AbstractValidator<AddAttributeOptionCommand>
{
    public AddAttributeOptionCommandValidator()
    {
        RuleFor(command => command.ProductTypeId)
            .NotEqual(Guid.Empty);

        RuleFor(command => command.AttributeDefinitionId)
            .NotEqual(Guid.Empty);

        RuleFor(command => command.Code)
            .NotEmpty()
            .Must(BeValidOptionCode)
            .WithMessage(
                "Option code must be lowercase kebab-case, such as " +
                "'space-black', 'medium', or '250g'.");

        RuleFor(command => command.DisplayOrder)
            .GreaterThanOrEqualTo(0);
    }

    private static bool BeValidOptionCode(string? value)
    {
        try
        {
            _ = AttributeOptionCode.Create(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}