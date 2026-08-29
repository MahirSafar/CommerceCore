using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.DeactivateProduct;

public sealed class DeactivateProductCommandValidator : AbstractValidator<DeactivateProductCommand>
{
    public DeactivateProductCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty(); 
    }
}
