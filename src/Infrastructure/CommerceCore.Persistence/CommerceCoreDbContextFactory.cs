using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CommerceCore.Persistence;

public sealed class CommerceCoreDbContextFactory
    : IDesignTimeDbContextFactory<CommerceCoreDbContext>
{
    private const string ConnectionStringEnvironmentVariable =
        "COMMERCECORE_MIGRATIONS_CONNECTION_STRING";

    public CommerceCoreDbContext CreateDbContext(string[] args)
    {
        string? connectionString = Environment.GetEnvironmentVariable(
            ConnectionStringEnvironmentVariable);

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Set {ConnectionStringEnvironmentVariable} before running EF Core tooling.");
        }

        DbContextOptions<CommerceCoreDbContext> options =
            new DbContextOptionsBuilder<CommerceCoreDbContext>()
                .UseNpgsql(connectionString)
                .Options;

        return new CommerceCoreDbContext(options);
    }
}
