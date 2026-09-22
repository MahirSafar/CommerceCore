using FluentValidation;

namespace CommerceCore.Application.Catalog.Products.Queries.ListStorefrontProducts;

public sealed class ListStorefrontProductsQueryValidator : AbstractValidator<ListStorefrontProductsQuery>
{
    public ListStorefrontProductsQueryValidator()
    {
        RuleFor(query => query.PageSize)
            .InclusiveBetween(
                1,
                ListStorefrontProductsQuery.MaximumPageSize);

        RuleFor(query => query.AfterProductId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("The cursor cannot be an empty GUID.");

        RuleFor(query => query.ProductTypeId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("The product type ID cannot be an empty GUID.");
    }
}
