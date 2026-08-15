using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Commands.ArchiveProduct;

public sealed class ArchiveProductCommandValidator : AbstractValidator<ArchiveProductCommand>
{
    public ArchiveProductCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty();
    }
}
