namespace CommerceCore.Persistence.Products;

internal static class StorefrontProductDetailsSql
{
    internal const string Query = """
        SELECT
            p.id AS "ProductId",
            p.product_type_id AS "ProductTypeId",
            p.name::text AS "NameJson",
            p.price_amount AS "BasePriceAmount",
            p.price_currency AS "Currency",
            v.id AS "VariantId",
            v.sku AS "VariantSku",
            v.price_amount AS "VariantPriceAmount",
            v.price_currency AS "VariantCurrency",
            v.is_default AS "IsDefault",
            v.options::text AS "OptionsJson"
        FROM (
            SELECT
                id,
                tenant_id,
                product_type_id,
                name,
                price_amount,
                price_currency
            FROM catalog.products
            WHERE id = @productId
              AND tenant_id = @tenantId
              AND status = 'Active'
              AND NOT is_deleted
            LIMIT 2
        ) AS p
        LEFT JOIN LATERAL (
            SELECT
                pv.id,
                pv.sku,
                pv.price_amount,
                pv.price_currency,
                pv.is_default,
                pv.options
            FROM catalog.product_variants AS pv
            WHERE pv.tenant_id = @tenantId
              AND pv.tenant_id = p.tenant_id
              AND pv.product_id = p.id
              AND pv.product_id = @productId
              AND pv.status = 'Active'
              AND (@afterVariantId IS NULL OR pv.id > @afterVariantId)
            ORDER BY pv.id
            LIMIT @take
        ) AS v ON true
        ORDER BY p.id, v.id
        """;

    internal sealed record Row(
        Guid ProductId,
        Guid ProductTypeId,
        string NameJson,
        decimal BasePriceAmount,
        string Currency,
        Guid? VariantId,
        string? VariantSku,
        decimal? VariantPriceAmount,
        string? VariantCurrency,
        bool? IsDefault,
        string? OptionsJson);
}
