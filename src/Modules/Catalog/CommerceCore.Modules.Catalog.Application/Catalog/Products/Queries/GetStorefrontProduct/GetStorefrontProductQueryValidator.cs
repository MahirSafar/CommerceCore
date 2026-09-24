using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Queries.GetStorefrontProduct;

public sealed class GetStorefrontProductQueryValidator : AbstractValidator<GetStorefrontProductQuery>
{
    public GetStorefrontProductQueryValidator()
    {
        RuleFor(query => query.ProductId).NotEmpty();
        RuleFor(query => query.VariantPageSize)
            .InclusiveBetween(
                1,
                GetStorefrontProductQuery.MaximumVariantPageSize);
        RuleFor(query => query.AfterVariantId)
            .Must(value => value is null || value != Guid.Empty)
            .WithMessage("AfterVariantId cannot be an empty GUID.");
    }
}
