using CommerceCore.Application.Common.Abstractions.Persistence;
using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence;

public sealed class CommerceCoreDbContext(DbContextOptions<CommerceCoreDbContext> options) 
    : DbContext(options), ICommerceCoreDbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductType> ProductTypes => Set<ProductType>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("ltree");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceCoreDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
