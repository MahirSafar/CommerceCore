using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.RestoreProduct;

public sealed class RestoreProductCommandValidator : AbstractValidator<RestoreProductCommand>
{
    public RestoreProductCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}