using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using FluentValidation;

namespace CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;

public sealed class DefineAttributeCommandValidator
    : AbstractValidator<DefineAttributeCommand>
{
    public DefineAttributeCommandValidator()
    {
        RuleFor(command => command.ProductTypeId)
            .NotEqual(Guid.Empty);

        RuleFor(command => command.Key)
            .NotEmpty()
            .Must(BeValidAttributeKey)
            .WithMessage(
                "Attribute key must be lowercase snake_case, start with a letter, " +
                "end with a letter or digit, and be at most 64 characters.");

        RuleFor(command => command.DataType)
            .IsInEnum();

        RuleFor(command => command.Scope)
            .IsInEnum();

        RuleFor(command => command.DisplayOrder)
            .GreaterThanOrEqualTo(0);

        RuleFor(command => command)
            .Must(HasValidNumericConstraints)
            .WithMessage(
                "Numeric constraints are only valid for Integer, Decimal, and Measurement attributes.");

        RuleFor(command => command)
            .Must(HasValidNumericRange)
            .WithMessage("Minimum value cannot be greater than maximum value.");

        RuleFor(command => command)
            .Must(HasIntegralIntegerRange)
            .WithMessage("Integer attribute range values must be whole numbers.");

        RuleFor(command => command)
            .Must(HasValidLengthConstraints)
            .WithMessage("Length constraints are only valid for Text attributes.");

        RuleFor(command => command)
            .Must(HasValidLengthRange)
            .WithMessage("Minimum length cannot be greater than maximum length.");

        RuleFor(command => command)
            .Must(HasValidMeasurementUnitFamily)
            .WithMessage(
                "Measurement attributes require a valid lowercase snake_case measurement unit family; " +
                "other types cannot define one.");
    }

    private static bool BeValidAttributeKey(string? value)
    {
        try
        {
            _ = AttributeKey.Create(value ?? string.Empty);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool HasValidNumericConstraints(DefineAttributeCommand command) =>
        !(command.MinimumValue.HasValue || command.MaximumValue.HasValue) ||
               command.DataType is AttributeDataType.Integer
                   or AttributeDataType.Decimal
                   or AttributeDataType.Measurement;

    private static bool HasValidNumericRange(DefineAttributeCommand command) =>
        !command.MinimumValue.HasValue ||
        !command.MaximumValue.HasValue ||
        command.MinimumValue <= command.MaximumValue;

    private static bool HasIntegralIntegerRange(DefineAttributeCommand command) =>
         command.DataType != AttributeDataType.Integer || (!command.MinimumValue.HasValue ||
                decimal.Truncate(command.MinimumValue.Value) == command.MinimumValue.Value) &&
               (!command.MaximumValue.HasValue ||
                decimal.Truncate(command.MaximumValue.Value) == command.MaximumValue.Value);

    private static bool HasValidLengthConstraints(DefineAttributeCommand command) =>
        !(command.MinimumLength.HasValue || command.MaximumLength.HasValue) ||
               command.DataType == AttributeDataType.Text;

    private static bool HasValidLengthRange(DefineAttributeCommand command) =>
        (!command.MinimumLength.HasValue ||
         !command.MaximumLength.HasValue ||
         command.MinimumLength <= command.MaximumLength) &&
        (!command.MinimumLength.HasValue || command.MinimumLength >= 0) &&
        (!command.MaximumLength.HasValue || command.MaximumLength >= 0);

    private static bool HasValidMeasurementUnitFamily(DefineAttributeCommand command)
    {
        if (command.DataType == AttributeDataType.Measurement)
        {
            if (string.IsNullOrWhiteSpace(command.MeasurementUnitFamily))
                return false;

            try
            {
                _ = MeasurementUnitFamily.Create(command.MeasurementUnitFamily);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        return string.IsNullOrWhiteSpace(command.MeasurementUnitFamily);
    }
}