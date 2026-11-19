using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

/// <summary>
/// Pins what the API's runtime role may do after docker/postgres/init/00-provision-app-role.sql,
/// in both orders the script runs in: before migrations, as on the first start of the
/// Compose database, and after them, as on a database that already has its tables.
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
public sealed class AppRolePrivilegeIntegrationTests(PostgreSqlFixture fixture)
{
    private const string ControlPlaneSchema = "platform";

    // Every privilege of every table, sequence and schema outside the system schemas and public,
    // so that a new module schema missing from the script fails here.
    private const string PrivilegesSql = """
        WITH application_schemas AS (
            SELECT n.oid, n.nspname
            FROM pg_catalog.pg_namespace AS n
            WHERE n.nspname NOT IN ('pg_catalog', 'information_schema', 'public')
              AND n.nspname NOT LIKE 'pg\_%'
        )
        SELECT 'table',
               s.nspname::text,
               format('%I.%I', s.nspname, c.relname),
               p.privilege,
               has_table_privilege(current_user, c.oid, p.privilege)
        FROM application_schemas AS s
        JOIN pg_catalog.pg_class AS c
            ON c.relnamespace = s.oid AND c.relkind IN ('r', 'p')
        CROSS JOIN unnest(ARRAY['SELECT', 'INSERT', 'UPDATE', 'DELETE',
                                'TRUNCATE', 'REFERENCES', 'TRIGGER', 'MAINTAIN']) AS p(privilege)
        UNION ALL
        SELECT 'sequence',
               s.nspname::text,
               format('%I.%I', s.nspname, c.relname),
               p.privilege,
               has_sequence_privilege(current_user, c.oid, p.privilege)
        FROM application_schemas AS s
        JOIN pg_catalog.pg_class AS c
            ON c.relnamespace = s.oid AND c.relkind = 'S'
        CROSS JOIN unnest(ARRAY['USAGE', 'SELECT', 'UPDATE']) AS p(privilege)
        UNION ALL
        SELECT 'schema',
               s.nspname::text,
               format('%I', s.nspname),
               p.privilege,
               has_schema_privilege(current_user, s.oid, p.privilege)
        FROM application_schemas AS s
        CROSS JOIN unnest(ARRAY['USAGE', 'CREATE']) AS p(privilege);
        """;

    // Objects this test was written against; the query must find all of them.
    private static readonly string[] KnownObjects =
    [
        "catalog",
        "catalog.products",
        "catalog.schema_revision_seq",
        "outbox",
        "outbox.messages",
        "platform",
        "platform.storefronts",
        "platform.tenant_memberships",
        "platform.tenants",
    ];

    [Fact]
    public async Task AppRole_ProvisionedAfterMigrations_HasExactPrivileges()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await AssertExactPrivilegesAsync(
            fixture.ApplicationConnectionString,
            cancellationToken);
    }

    [Fact]
    public async Task AppRole_ProvisionedBeforeMigrations_HasExactPrivileges()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string databaseName = $"provisioning_{Guid.NewGuid():N}";

        await ExecuteAsAdminAsync($"CREATE DATABASE {databaseName}", cancellationToken);

        try
        {
            string adminConnectionString =
                ForDatabase(fixture.AdminConnectionString, databaseName);

            await PostgreSqlFixture.ProvisionAppRoleAsync(
                adminConnectionString,
                cancellationToken);

            DbContextOptions<CommerceCoreDbContext> options =
                new DbContextOptionsBuilder<CommerceCoreDbContext>()
                    .UseNpgsql(adminConnectionString)
                    .Options;

            await using (var dbContext = new CommerceCoreDbContext(options))
            {
                await dbContext.Database.MigrateAsync(cancellationToken);
            }

            await AssertExactPrivilegesAsync(
                ForDatabase(fixture.ApplicationConnectionString, databaseName),
                cancellationToken);
        }
        finally
        {
            await ExecuteAsAdminAsync(
                $"DROP DATABASE {databaseName} WITH (FORCE)",
                CancellationToken.None);
        }
    }

    [Fact]
    public async Task AppRole_AfterProvisioning_IsRestrictedAndOwnsNothing()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        const string sql = """
            SELECT r.rolname,
                   r.rolcanlogin,
                   r.rolsuper OR r.rolcreatedb OR r.rolcreaterole OR r.rolinherit
                       OR r.rolreplication OR r.rolbypassrls,
                   (SELECT count(*) FROM pg_catalog.pg_auth_members AS m WHERE m.member = r.oid),
                   (SELECT count(*) FROM pg_catalog.pg_class AS c WHERE c.relowner = r.oid),
                   (SELECT count(*) FROM pg_catalog.pg_namespace AS n WHERE n.nspowner = r.oid)
            FROM pg_catalog.pg_roles AS r
            WHERE r.rolname = current_user;
            """;

        await using var connection = new NpgsqlConnection(fixture.ApplicationConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.Equal("commercecore_app", reader.GetString(0));
        Assert.True(reader.GetBoolean(1), "The role must be able to log in.");
        Assert.False(reader.GetBoolean(2), "The role must have no elevated attribute.");
        Assert.Equal(0L, reader.GetInt64(3));
        Assert.Equal(0L, reader.GetInt64(4));
        Assert.Equal(0L, reader.GetInt64(5));
    }

    [Fact]
    public async Task ProvisioningScript_WhenRepeated_KeepsPasswordAndPrivileges()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        await PostgreSqlFixture.ProvisionAppRoleAsync(
            fixture.AdminConnectionString,
            cancellationToken);

        // AssertExactPrivilegesAsync opens an unpooled connection, so the password is checked again.
        await AssertExactPrivilegesAsync(
            fixture.ApplicationConnectionString,
            cancellationToken);
    }

    [Theory]
    [InlineData("SUPERUSER", "NOSUPERUSER")]
    [InlineData("CREATEDB", "NOCREATEDB")]
    [InlineData("CREATEROLE", "NOCREATEROLE")]
    [InlineData("INHERIT", "NOINHERIT")]
    [InlineData("REPLICATION", "NOREPLICATION")]
    [InlineData("BYPASSRLS", "NOBYPASSRLS")]
    [InlineData("NOLOGIN", "LOGIN")]
    public async Task ProvisioningScript_WhenRoleIsNotRestricted_Fails(
        string attribute,
        string restoredAttribute)
    {
        await AssertProvisioningRejectedAsync(
            $"ALTER ROLE commercecore_app {attribute}",
            $"ALTER ROLE commercecore_app {restoredAttribute}",
            TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task ProvisioningScript_WhenRoleHasMembership_Fails()
    {
        await AssertProvisioningRejectedAsync(
            "GRANT pg_read_all_data TO commercecore_app",
            "REVOKE pg_read_all_data FROM commercecore_app",
            TestContext.Current.CancellationToken);
    }

    private async Task AssertProvisioningRejectedAsync(
        string changeRole,
        string restoreRole,
        CancellationToken cancellationToken)
    {
        await ExecuteAsAdminAsync(changeRole, cancellationToken);

        try
        {
            PostgresException exception = await Assert.ThrowsAsync<PostgresException>(
                () => PostgreSqlFixture.ProvisionAppRoleAsync(
                    fixture.AdminConnectionString,
                    cancellationToken));

            Assert.Equal(PostgresErrorCodes.RaiseException, exception.SqlState);
            Assert.Contains("dedicated restricted role", exception.MessageText);
        }
        finally
        {
            await ExecuteAsAdminAsync(restoreRole, CancellationToken.None);
        }
    }

    private static async Task AssertExactPrivilegesAsync(
        string applicationConnectionString,
        CancellationToken cancellationToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(applicationConnectionString)
        {
            Pooling = false
        };

        await using var connection = new NpgsqlConnection(connectionString.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(PrivilegesSql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var objects = new HashSet<string>(StringComparer.Ordinal);
        var violations = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new PrivilegeRow(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetBoolean(4));

            objects.Add(row.Name);

            if (row.IsHeld != IsExpected(row))
            {
                violations.Add(
                    $"{row.Kind} {row.Name}: {row.Privilege} is " +
                    (row.IsHeld ? "granted but must not be" : "missing"));
            }
        }

        Assert.Empty(KnownObjects.Except(objects, StringComparer.Ordinal));
        Assert.Empty(violations);
    }

    private static bool IsExpected(PrivilegeRow row) => row.Kind switch
    {
        "table" when row.Schema == ControlPlaneSchema => row.Privilege is "SELECT",
        "table" => row.Privilege is "SELECT" or "INSERT" or "UPDATE" or "DELETE",
        "sequence" when row.Schema == ControlPlaneSchema => false,
        "sequence" => row.Privilege is "USAGE" or "SELECT",
        "schema" => row.Privilege is "USAGE",
        _ => throw new InvalidOperationException($"Unknown object kind {row.Kind}."),
    };

    private async Task ExecuteAsAdminAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        await using var connection = new NpgsqlConnection(fixture.AdminConnectionString);
        await connection.OpenAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string ForDatabase(string connectionString, string databaseName) =>
        new NpgsqlConnectionStringBuilder(connectionString)
        {
            Database = databaseName
        }.ConnectionString;

    private sealed record PrivilegeRow(
        string Kind,
        string Schema,
        string Name,
        string Privilege,
        bool IsHeld);
}