using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.Schema;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Platform.Contracts;
using Mediator;

namespace CommerceCore.Application.Catalog.ProductTypes.Queries.GetProductTypeSchema;

public sealed class GetProductTypeSchemaQueryHandler(
    IProductTypeEffectiveSchemaReader reader,
    ITenantContext tenantContext)
    : IQueryHandler<GetProductTypeSchemaQuery, ProductTypeSchemaResult?>
{
    public async ValueTask<ProductTypeSchemaResult?> Handle(
        GetProductTypeSchemaQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        cancellationToken.ThrowIfCancellationRequested();

        if (!tenantContext.IsResolved ||
            tenantContext.TenantId is not TenantId tenantId ||
            tenantId.Value == Guid.Empty)
        {
            throw new InvalidOperationException(
                "A resolved tenant is required to read a product-type schema.");
        }

        ProductTypeId productTypeId = ProductTypeId.From(query.ProductTypeId);

        EffectiveProductTypeSchema? schema = await reader.GetAsync(
            productTypeId,
            cancellationToken);

        if (schema is null)
        {
            return null;
        }

        return new ProductTypeSchemaResult(
            productTypeId.Value,
            schema.EffectiveSchemaVersion,
            schema.Attributes.Select(MapAttribute).ToArray());
    }

    private static ProductTypeAttributeSchema MapAttribute(
        EffectiveAttributeDefinition attribute)
    {
        return new ProductTypeAttributeSchema(
            attribute.Key.Value,
            MapDataType(attribute.DataType),
            MapScope(attribute.Scope),
            attribute.IsRequired,
            MapEnforcementStatus(attribute.EnforcementStatus),
            attribute.IsDeprecated,
            attribute.MinimumLength,
            attribute.MaximumLength,
            attribute.MinimumValue,
            attribute.MaximumValue,
            attribute.MeasurementUnitFamily?.Value,
            attribute.Options
                .Select(option => new ProductTypeAttributeOptionSchema(
                    option.Code.Value,
                    option.IsDeprecated))
                .ToArray());
    }

    private static string MapDataType(AttributeDataType value) =>
        value switch
        {
            AttributeDataType.Text => "text",
            AttributeDataType.Integer => "integer",
            AttributeDataType.Decimal => "decimal",
            AttributeDataType.Boolean => "boolean",
            AttributeDataType.SingleSelect => "single_select",
            AttributeDataType.MultiSelect => "multi_select",
            AttributeDataType.Measurement => "measurement",
            _ => throw new InvalidOperationException(
                $"Unsupported attribute data type: {value}.")
        };

    private static string MapScope(AttributeScope value) =>
        value switch
        {
            AttributeScope.ProductSpecification => "product_specification",
            AttributeScope.VariantOption => "variant_option",
            _ => throw new InvalidOperationException(
                $"Unsupported attribute scope: {value}.")
        };

    private static string MapEnforcementStatus(
        AttributeEnforcementStatus value) =>
        value switch
        {
            AttributeEnforcementStatus.Draft => "draft",
            AttributeEnforcementStatus.Backfilling => "backfilling",
            AttributeEnforcementStatus.Enforced => "enforced",
            _ => throw new InvalidOperationException(
                $"Unsupported attribute enforcement status: {value}.")
        };
}
