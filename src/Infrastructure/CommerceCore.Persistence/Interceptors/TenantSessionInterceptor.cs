using System.Data.Common;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CommerceCore.Persistence.Interceptors;

public sealed class TenantSessionInterceptor(ITenantContext? tenantContext = null) : DbConnectionInterceptor
{
    private readonly ITenantContext? _tenantContext = tenantContext;

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantSession(connection);
        base.ConnectionOpened(connection, eventData);
    }

    public override async Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        await SetTenantSessionAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    private void SetTenantSession(DbConnection connection)
    {
        var tenantIdString = _tenantContext?.TenantId?.Value.ToString() ?? string.Empty;
        using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT set_config('app.tenant_id', '{tenantIdString}', false);";
        cmd.ExecuteNonQuery();
    }

    private async Task SetTenantSessionAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        var tenantIdString = _tenantContext?.TenantId?.Value.ToString() ?? string.Empty;
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = $"SELECT set_config('app.tenant_id', '{tenantIdString}', false);";
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
