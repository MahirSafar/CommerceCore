using CommerceCore.Domain.Catalog.Products;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence;

public sealed class CommerceCoreDbContext(DbContextOptions<CommerceCoreDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceCoreDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
