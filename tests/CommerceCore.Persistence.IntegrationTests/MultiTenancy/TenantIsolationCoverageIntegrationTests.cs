using CommerceCore.Application.Catalog.ProductTypes.Commands.AddAttributeOption;
using CommerceCore.Application.Catalog.ProductTypes.Commands.CreateProductType;
using CommerceCore.Application.Catalog.ProductTypes.Commands.DefineAttribute;
using CommerceCore.Domain.Catalog.Attributes.ValueObjects;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Catalog.ProductTypes.Enums;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Platform.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CommerceCore.Persistence.IntegrationTests.MultiTenancy;

/// <summary>
/// Pins the database-level tenant isolation contract of every tenant-scoped table.
/// Tables are discovered from the PostgreSQL catalog, so a new tenant table is checked
/// without editing this class; the behavioural probes then need a seeded row for it
/// (see <see cref="SeedOwnerRowsAsync"/>).
/// </summary>
[Collection(nameof(PostgreSqlCollection))]
public sealed class TenantIsolationCoverageIntegrationTests(
    PostgreSqlFixture fixture)
{
    private const string TenantIsolationPolicyName = "tenant_isolation_policy";

    // How PostgreSQL 18 deparses the migration's policy expression
    // tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid.
    private const string TenantIsolationExpression =
        "(tenant_id = (NULLIF(current_setting('app.tenant_id'::text, true), ''::text))::uuid)";

    // Every table with a tenant_id column outside the control-plane schema.
    private const string TenantTablesSql = """
        SELECT format('%I.%I', n.nspname, c.relname),
               c.relrowsecurity,
               c.relforcerowsecurity
        FROM pg_catalog.pg_class AS c
        JOIN pg_catalog.pg_namespace AS n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p')
          AND n.nspname NOT IN ('pg_catalog', 'information_schema', 'platform')
          AND n.nspname NOT LIKE 'pg\_%'
          AND EXISTS (
              SELECT 1
              FROM pg_catalog.pg_attribute AS a
              WHERE a.attrelid = c.oid
                AND a.attname = 'tenant_id'
                AND NOT a.attisdropped)
        ORDER BY 1;
        """;

    private const string PoliciesSql = """
        SELECT format('%I.%I', p.schemaname, p.tablename),
               p.policyname,
               p.permissive,
               array_to_string(p.roles, ','),
               p.cmd,
               p.qual,
               p.with_check
        FROM pg_catalog.pg_policies AS p;
        """;

    // The tables this test was written against; the discovery query must find all of them.
    private static readonly string[] KnownTenantTables =
    [
        "catalog.attribute_definitions",
        "catalog.attribute_options",
        "catalog.product_type_effective_schema",
        "catalog.product_types",
        "catalog.product_variants",
        "catalog.products",
        "outbox.messages",
    ];

    [Fact]
    public async Task TenantScopedTables_AllHaveForcedTenantIsolationPolicy()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        IReadOnlyList<TenantTable> tables = await ReadTenantTablesAsync(cancellationToken);
        ILookup<string, TenantPolicy> policies =
            (await ReadPoliciesAsync(cancellationToken))
            .ToLookup(policy => policy.TableName, StringComparer.Ordinal);

        string[] undiscoveredKnownTables = KnownTenantTables
            .Except(tables.Select(table => table.Name), StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(undiscoveredKnownTables);

        var violations = new List<string>();

        foreach (TenantTable table in tables)
        {
            if (!table.RowSecurityEnabled)
            {
                violations.Add($"{table.Name}: row-level security is not enabled");
            }

            if (!table.RowSecurityForced)
            {
                violations.Add($"{table.Name}: row-level security is not forced");
            }

            TenantPolicy[] tablePolicies = policies[table.Name].ToArray();

            if (tablePolicies.Length != 1)
            {
                violations.Add(
                    $"{table.Name}: expected exactly one policy, found {tablePolicies.Length} " +
                    $"[{string.Join(", ", tablePolicies.Select(policy => policy.Name))}]");

                continue;
            }

            TenantPolicy tablePolicy = tablePolicies[0];

            if (tablePolicy.Name != TenantIsolationPolicyName)
            {
                violations.Add($"{table.Name}: policy is named {tablePolicy.Name}");
            }

            if (tablePolicy.Permissive != "PERMISSIVE")
            {
                violations.Add($"{table.Name}: policy is {tablePolicy.Permissive}");
            }

            if (tablePolicy.Roles != "public")
            {
                violations.Add($"{table.Name}: policy applies to {tablePolicy.Roles}");
            }

            if (tablePolicy.Command != "ALL")
            {
                violations.Add($"{table.Name}: policy covers {tablePolicy.Command} only");
            }

            if (tablePolicy.Using != TenantIsolationExpression)
            {
                violations.Add($"{table.Name}: USING is {tablePolicy.Using ?? "<none>"}");
            }

            if (tablePolicy.WithCheck != TenantIsolationExpression)
            {
                violations.Add($"{table.Name}: WITH CHECK is {tablePolicy.WithCheck ?? "<none>"}");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public async Task SchemaEngineDatabaseObjects_AfterMigration_Exist()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        const string sql = """
            SELECT
                EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_trigger AS t
                    WHERE t.tgrelid = to_regclass('catalog.product_types')
                      AND t.tgname = 'trg_product_types_set_path'
                      AND t.tgfoid = to_regprocedure('catalog.trg_fn_product_type_path()')
                      AND t.tgenabled = 'O'
                      AND NOT t.tgisinternal),
                to_regprocedure('catalog.jsonb_key_count(jsonb)') IS NOT NULL,
                EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_class AS c
                    WHERE c.oid = to_regclass('catalog.schema_revision_seq')
                      AND c.relkind = 'S'),
                EXISTS (
                    SELECT 1
                    FROM pg_catalog.pg_extension AS e
                    WHERE e.extname = 'ltree');
            """;

        await using NpgsqlConnection connection =
            await OpenApplicationConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(sql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        Assert.True(await reader.ReadAsync(cancellationToken));
        Assert.True(reader.GetBoolean(0), "Trigger trg_product_types_set_path is missing, disabled or not bound to catalog.trg_fn_product_type_path().");
        Assert.True(reader.GetBoolean(1), "Function catalog.jsonb_key_count(jsonb) is missing.");
        Assert.True(reader.GetBoolean(2), "Sequence catalog.schema_revision_seq is missing.");
        Assert.True(reader.GetBoolean(3), "Extension ltree is missing.");
    }

    [Fact]
    public async Task TenantScopedTables_OtherTenantSession_CannotReadUpdateOrDeleteRows()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        (TenantId owner, TenantId other) = await CreateTenantsWithOwnerRowsAsync(cancellationToken);
        IReadOnlyList<TenantTable> tables = await ReadTenantTablesAsync(cancellationToken);
        var violations = new List<string>();

        foreach (TenantTable table in tables)
        {
            string countOwnerRows = $"SELECT count(*) FROM {table.Name} WHERE tenant_id = @owner";
            string touchOwnerRows = $"UPDATE {table.Name} SET tenant_id = tenant_id WHERE tenant_id = @owner";
            string deleteOwnerRows = $"DELETE FROM {table.Name} WHERE tenant_id = @owner";

            long ownerCount = await ScalarAsync<long>(owner, countOwnerRows, owner, other, null, cancellationToken);

            if (ownerCount == 0)
            {
                violations.Add($"{table.Name}: no row seeded for the owner tenant; extend SeedOwnerRowsAsync");
                continue;
            }

            long otherCount = await ScalarAsync<long>(other, countOwnerRows, owner, other, null, cancellationToken);
            ProbeResult ownerUpdate = await ExecuteAsync(owner, touchOwnerRows, owner, other, null, cancellationToken);
            ProbeResult otherUpdate = await ExecuteAsync(other, touchOwnerRows, owner, other, null, cancellationToken);
            ProbeResult otherDelete = await ExecuteAsync(other, deleteOwnerRows, owner, other, null, cancellationToken);

            if (otherCount != 0)
            {
                violations.Add($"{table.Name}: another tenant sees {otherCount} of the owner's {ownerCount} rows");
            }

            if (ownerUpdate.RowsAffected != ownerCount)
            {
                violations.Add($"{table.Name}: the owner's UPDATE probe affected {ownerUpdate}, expected {ownerCount} rows");
            }

            if (otherUpdate.RowsAffected != 0)
            {
                violations.Add($"{table.Name}: another tenant's UPDATE affected {otherUpdate}, expected 0 rows");
            }

            if (otherDelete.RowsAffected != 0)
            {
                violations.Add($"{table.Name}: another tenant's DELETE affected {otherDelete}, expected 0 rows");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public async Task TenantScopedTables_RowTaggedWithAnotherTenant_IsRejected()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        (TenantId owner, TenantId other) = await CreateTenantsWithOwnerRowsAsync(cancellationToken);
        IReadOnlyList<TenantTable> tables = await ReadTenantTablesAsync(cancellationToken);
        var violations = new List<string>();

        foreach (TenantTable table in tables)
        {
            string? ownerRow = await ReadOwnerRowAsJsonAsync(table, owner, other, cancellationToken);

            if (ownerRow is null)
            {
                violations.Add($"{table.Name}: no row seeded for the owner tenant; extend SeedOwnerRowsAsync");
                continue;
            }

            string insertCopy = $"INSERT INTO {table.Name} SELECT * FROM json_populate_record(NULL::{table.Name}, @row::json)";
            string retagOwnerRows = $"UPDATE {table.Name} SET tenant_id = @other WHERE tenant_id = @owner";

            // The owner may write its own tenant id, so its copy passes the policy and stops at the primary key:
            // this proves the INSERT probe is well formed before it is used against the other tenant.
            ProbeResult ownerInsert = await ExecuteAsync(owner, insertCopy, owner, other, ownerRow, cancellationToken);
            ProbeResult otherInsert = await ExecuteAsync(other, insertCopy, owner, other, ownerRow, cancellationToken);
            ProbeResult ownerRetag = await ExecuteAsync(owner, retagOwnerRows, owner, other, null, cancellationToken);

            if (ownerInsert.SqlState != PostgresErrorCodes.UniqueViolation)
            {
                violations.Add($"{table.Name}: the owner's INSERT probe gave {ownerInsert}, expected SQLSTATE 23505");
            }

            if (otherInsert.SqlState != PostgresErrorCodes.InsufficientPrivilege)
            {
                violations.Add($"{table.Name}: another tenant's INSERT of a row tagged with the owner gave {otherInsert}, expected SQLSTATE 42501");
            }

            if (ownerRetag.SqlState != PostgresErrorCodes.InsufficientPrivilege)
            {
                violations.Add($"{table.Name}: the owner's UPDATE to another tenant id gave {ownerRetag}, expected SQLSTATE 42501");
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public async Task TenantScopedTables_WithoutTenant_HideRowsAndRejectInsert()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        (TenantId owner, TenantId other) = await CreateTenantsWithOwnerRowsAsync(cancellationToken);
        IReadOnlyList<TenantTable> tables = await ReadTenantTablesAsync(cancellationToken);
        var violations = new List<string>();

        foreach (TenantTable table in tables)
        {
            string? ownerRow = await ReadOwnerRowAsJsonAsync(table, owner, other, cancellationToken);

            if (ownerRow is null)
            {
                violations.Add($"{table.Name}: no row seeded for the owner tenant; extend SeedOwnerRowsAsync");
                continue;
            }

            string countAllRows = $"SELECT count(*) FROM {table.Name}";
            string insertCopy = $"INSERT INTO {table.Name} SELECT * FROM json_populate_record(NULL::{table.Name}, @row::json)";

            long visibleRows = await ScalarAsync<long>(null, countAllRows, owner, other, null, cancellationToken);
            ProbeResult insert = await ExecuteAsync(null, insertCopy, owner, other, ownerRow, cancellationToken);

            if (visibleRows != 0)
            {
                violations.Add($"{table.Name}: a session without a tenant sees {visibleRows} rows");
            }

            if (insert.SqlState != PostgresErrorCodes.InsufficientPrivilege)
            {
                violations.Add($"{table.Name}: an INSERT without a tenant gave {insert}, expected SQLSTATE 42501");
            }
        }

        Assert.Empty(violations);
    }

    private async Task<(TenantId Owner, TenantId Other)> CreateTenantsWithOwnerRowsAsync(
        CancellationToken cancellationToken)
    {
        // Fresh tenants keep the probes independent of rows written by other tests.
        TenantId owner = await fixture.CreateTenantAsync(cancellationToken);
        TenantId other = await fixture.CreateTenantAsync(cancellationToken);

        await SeedOwnerRowsAsync(owner, cancellationToken);

        return (owner, other);
    }

    // Writes at least one row of the owner tenant into every tenant-scoped table, through the
    // real handlers and the schema coordinator. A new tenant table needs a seed step here.
    // Only a root product type is created: the path trigger looks up the parent of a child type,
    // which another tenant cannot see, and would fail before the policy is evaluated.
    private async Task SeedOwnerRowsAsync(
        TenantId owner,
        CancellationToken cancellationToken)
    {
        fixture.Services.GetRequiredService<TestTenantContext>().SetTenant(owner);

        await using AsyncServiceScope scope = fixture.Services.CreateAsyncScope();

        CommerceCoreDbContext dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        IProductTypeSchemaCoordinator schemaCoordinator = scope.ServiceProvider
            .GetRequiredService<IProductTypeSchemaCoordinator>();

        // catalog.product_types and catalog.product_type_effective_schema
        CreateProductTypeResult productType = await new CreateProductTypeCommandHandler(
                dbContext,
                schemaCoordinator,
                scope.ServiceProvider.GetRequiredService<ITenantContext>())
            .Handle(
                new CreateProductTypeCommand(
                    $"isolation_{Guid.NewGuid():N}",
                    ParentProductTypeId: null,
                    IsAssignable: true),
                cancellationToken);

        // catalog.attribute_definitions
        DefineAttributeResult attribute = await new DefineAttributeCommandHandler(
                dbContext,
                schemaCoordinator,
                scope.ServiceProvider.GetRequiredService<IAttributeDefinitionRegistry>())
            .Handle(
                new DefineAttributeCommand(
                    productType.ProductTypeId,
                    Key: "color",
                    DataType: AttributeDataType.SingleSelect,
                    Scope: AttributeScope.VariantOption,
                    IsRequired: false,
                    DisplayOrder: 0,
                    MinimumValue: null,
                    MaximumValue: null,
                    MinimumLength: null,
                    MaximumLength: null,
                    MeasurementUnitFamily: null),
                cancellationToken);

        // catalog.attribute_options
        await new AddAttributeOptionCommandHandler(dbContext, schemaCoordinator)
            .Handle(
                new AddAttributeOptionCommand(
                    productType.ProductTypeId,
                    attribute.AttributeDefinitionId,
                    Code: "black",
                    DisplayOrder: 0),
                cancellationToken);

        // catalog.products, catalog.product_variants and, through the created event, outbox.messages
        LanguageCode english = LanguageCode.Create("en");
        Money price = Money.Create(10m, "USD");

        Product product = Product.Create(
            owner,
            LocalizedText.Create(english, new Dictionary<LanguageCode, string> { [english] = "Isolation probe" }),
            price,
            ProductTypeId.From(productType.ProductTypeId),
            DateTimeOffset.UtcNow);

        product.AddVariant(
            VariantSku.Create($"ISO-{Guid.NewGuid():N}"),
            price,
            AttributeValueBag.Empty,
            isDefault: true);

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<string?> ReadOwnerRowAsJsonAsync(
        TenantTable table,
        TenantId owner,
        TenantId other,
        CancellationToken cancellationToken) =>
        await ScalarAsync<string?>(
            owner,
            $"SELECT row_to_json(r)::text FROM {table.Name} AS r WHERE r.tenant_id = @owner LIMIT 1",
            owner,
            other,
            null,
            cancellationToken);

    private async Task<IReadOnlyList<TenantTable>> ReadTenantTablesAsync(
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await OpenApplicationConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(TenantTablesSql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var tables = new List<TenantTable>();

        while (await reader.ReadAsync(cancellationToken))
        {
            tables.Add(new TenantTable(
                reader.GetString(0),
                reader.GetBoolean(1),
                reader.GetBoolean(2)));
        }

        return tables;
    }

    private async Task<IReadOnlyList<TenantPolicy>> ReadPoliciesAsync(
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await OpenApplicationConnectionAsync(cancellationToken);

        await using var command = new NpgsqlCommand(PoliciesSql, connection);
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken);

        var policies = new List<TenantPolicy>();

        while (await reader.ReadAsync(cancellationToken))
        {
            policies.Add(new TenantPolicy(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6)));
        }

        return policies;
    }

    private async Task<T> ScalarAsync<T>(
        TenantId? sessionTenant,
        string sql,
        TenantId owner,
        TenantId other,
        string? row,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await OpenApplicationConnectionAsync(cancellationToken);

        await using NpgsqlTransaction transaction =
            await BeginTenantTransactionAsync(connection, sessionTenant, cancellationToken);

        await using NpgsqlCommand command =
            CreateProbeCommand(connection, transaction, sql, owner, other, row);

        object? value = await command.ExecuteScalarAsync(cancellationToken);

        return value is null or DBNull ? default! : (T)value;
    }

    // Runs one statement as the runtime role in a transaction that is always rolled back,
    // so a probe never changes data even when isolation is broken.
    private async Task<ProbeResult> ExecuteAsync(
        TenantId? sessionTenant,
        string sql,
        TenantId owner,
        TenantId other,
        string? row,
        CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection =
            await OpenApplicationConnectionAsync(cancellationToken);

        await using NpgsqlTransaction transaction =
            await BeginTenantTransactionAsync(connection, sessionTenant, cancellationToken);

        await using NpgsqlCommand command =
            CreateProbeCommand(connection, transaction, sql, owner, other, row);

        try
        {
            return new ProbeResult(await command.ExecuteNonQueryAsync(cancellationToken), null);
        }
        catch (PostgresException exception)
        {
            return new ProbeResult(null, exception.SqlState);
        }
    }

    private static NpgsqlCommand CreateProbeCommand(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        TenantId owner,
        TenantId other,
        string? row)
    {
        var command = new NpgsqlCommand(sql, connection, transaction);

        command.Parameters.AddWithValue("owner", owner.Value);
        command.Parameters.AddWithValue("other", other.Value);
        command.Parameters.AddWithValue("row", (object?)row ?? DBNull.Value);

        return command;
    }

    private static async Task<NpgsqlTransaction> BeginTenantTransactionAsync(
        NpgsqlConnection connection,
        TenantId? sessionTenant,
        CancellationToken cancellationToken)
    {
        NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        // Transaction-local, so it ends with the rollback. An empty value means no tenant.
        await using var command = new NpgsqlCommand(
            "SELECT set_config('app.tenant_id', @tenant_id, true)",
            connection,
            transaction);

        command.Parameters.AddWithValue(
            "tenant_id",
            sessionTenant?.Value.ToString() ?? string.Empty);

        await command.ExecuteScalarAsync(cancellationToken);

        return transaction;
    }

    private async Task<NpgsqlConnection> OpenApplicationConnectionAsync(
        CancellationToken cancellationToken)
    {
        var connection = new NpgsqlConnection(fixture.ApplicationConnectionString);

        await connection.OpenAsync(cancellationToken);

        return connection;
    }

    private sealed record TenantTable(
        string Name,
        bool RowSecurityEnabled,
        bool RowSecurityForced);

    private sealed record TenantPolicy(
        string TableName,
        string Name,
        string Permissive,
        string Roles,
        string Command,
        string? Using,
        string? WithCheck);

    private sealed record ProbeResult(int? RowsAffected, string? SqlState)
    {
        public override string ToString() =>
            SqlState is null ? $"{RowsAffected} rows" : $"SQLSTATE {SqlState}";
    }
}