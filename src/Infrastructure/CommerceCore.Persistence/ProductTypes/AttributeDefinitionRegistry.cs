using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.ProductTypes.Exceptions;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using NpgsqlTypes;

namespace CommerceCore.Persistence.ProductTypes;

internal sealed class AttributeDefinitionRegistry(
    CommerceCoreDbContext dbContext)
    : IAttributeDefinitionRegistry
{
    private readonly CommerceCoreDbContext _dbContext = dbContext;

    public async Task EnsureKeyIsUniqueInHierarchyAsync(
        ProductTypeId productTypeId,
        AttributeKey attributeKey,
        CancellationToken cancellationToken = default)
    {
        IDbContextTransaction transaction = _dbContext.Database.CurrentTransaction
            ?? throw new InvalidOperationException(
                "Hierarchy validation must run inside the schema coordinator transaction.");

        NpgsqlConnection connection = _dbContext.Database.GetDbConnection()
            as NpgsqlConnection
            ?? throw new InvalidOperationException(
                "CommerceCore requires an Npgsql PostgreSQL connection.");

        NpgsqlTransaction dbTransaction = transaction.GetDbTransaction()
            as NpgsqlTransaction
            ?? throw new InvalidOperationException(
                "CommerceCore requires an Npgsql PostgreSQL transaction.");

        await using NpgsqlCommand command = new(
            """
            SELECT EXISTS (
                SELECT 1
                FROM catalog.product_types AS target
                INNER JOIN catalog.product_types AS related
                    ON related.path @> target.path
                    OR related.path <@ target.path
                INNER JOIN catalog.attribute_definitions AS definition
                    ON definition.product_type_id = related.id
                WHERE target.id = @product_type_id
                  AND related.id <> target.id
                  AND definition.key = @attribute_key
            );
            """,
            connection,
            dbTransaction);

        command.Parameters.Add(
            new NpgsqlParameter("product_type_id", NpgsqlDbType.Uuid)
            {
                Value = productTypeId.Value
            });

        command.Parameters.Add(
            new NpgsqlParameter("attribute_key", NpgsqlDbType.Text)
            {
                Value = attributeKey.Value
            });

        bool exists = (bool)(await command.ExecuteScalarAsync(cancellationToken)
            ?? throw new InvalidOperationException(
                "Could not validate attribute-key uniqueness."));

        if (exists)
        {
            throw new ProductTypeDomainException(
                "product_type.attribute_key_exists_in_hierarchy",
                $"Attribute key '{attributeKey}' already exists in the product type hierarchy.");
        }
    }
}