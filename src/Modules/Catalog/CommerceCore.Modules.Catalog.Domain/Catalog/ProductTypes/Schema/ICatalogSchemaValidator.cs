using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;

namespace CommerceCore.Domain.Catalog.ProductTypes.Schema;

public interface ICatalogSchemaValidator
{
    CatalogSchemaValidationResult ValidateProductSpecifications(
        AttributeValueBag currentSpecifications,
        AttributeValueBag proposedSpecifications,
        EffectiveProductTypeSchema schema);

    CatalogSchemaValidationResult ValidateVariantOptions(
        AttributeValueBag currentOptions,
        AttributeValueBag proposedOptions,
        EffectiveProductTypeSchema schema);
}

public sealed record CatalogSchemaValidationResult(
    IReadOnlyList<CatalogSchemaValidationError> Errors)
{
    public bool IsValid => Errors.Count == 0;
}

public sealed record CatalogSchemaValidationError(
    AttributeKey AttributeKey,
    string Code,
    string Message);