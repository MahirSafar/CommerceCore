using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Queries.GetProductById;

public sealed class GetProductByIdQueryValidator
    : AbstractValidator<GetProductByIdQuery>
{
    public GetProductByIdQueryValidator()
    {
        RuleFor(query => query.ProductId)
            .NotEmpty();
    }
}