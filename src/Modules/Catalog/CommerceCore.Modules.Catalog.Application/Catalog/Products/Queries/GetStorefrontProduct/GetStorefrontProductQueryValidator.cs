using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;

public sealed class GetStorefrontProductQueryValidator
    : AbstractValidator<GetStorefrontProductQuery>
{
    public GetStorefrontProductQueryValidator()
    {
        RuleFor(query => query.ProductId).NotEmpty();
    }
}
