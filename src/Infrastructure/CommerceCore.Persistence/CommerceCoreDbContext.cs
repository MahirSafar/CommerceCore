using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Modules.Catalog.Application.Common.Abstractions.Persistence;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Persistence.ProductTypes;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence;

public sealed class CommerceCoreDbContext(DbContextOptions<CommerceCoreDbContext> options) 
    : DbContext(options), ICommerceCoreDbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductType> ProductTypes => Set<ProductType>();

    public DbSet<ProductTypeEffectiveSchema> ProductTypeEffectiveSchemas => Set<ProductTypeEffectiveSchema>();

    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    public DbSet<Tenant> Tenants => Set<Tenant>();

    public DbSet<Storefront> Storefronts => Set<Storefront>();

    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("ltree");

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(CommerceCoreDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
