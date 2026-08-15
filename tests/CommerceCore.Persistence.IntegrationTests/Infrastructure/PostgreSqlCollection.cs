namespace CommerceCore.Persistence.IntegrationTests.Infrastructure;

[CollectionDefinition(nameof(PostgreSqlCollection))]
public sealed class PostgreSqlCollection
    : ICollectionFixture<PostgreSqlFixture>
{
}