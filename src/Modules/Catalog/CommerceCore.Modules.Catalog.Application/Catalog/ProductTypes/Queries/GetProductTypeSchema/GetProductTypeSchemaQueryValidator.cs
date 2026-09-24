using FluentValidation;

namespace CommerceCore.Application.Catalog.ProductTypes.Queries.GetProductTypeSchema;

public sealed class GetProductTypeSchemaQueryValidator
    : AbstractValidator<GetProductTypeSchemaQuery>
{
    public GetProductTypeSchemaQueryValidator()
    {
        RuleFor(query => query.ProductTypeId).NotEmpty();
    }
}
