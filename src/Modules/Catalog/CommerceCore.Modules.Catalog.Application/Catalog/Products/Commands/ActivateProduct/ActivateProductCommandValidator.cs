using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.ActivateProduct;

public sealed class ActivateProductCommandValidator : AbstractValidator<ActivateProductCommand>
{
    public ActivateProductCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}
