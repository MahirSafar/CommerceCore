using CommerceCore.Persistence.Configurations;
using CommerceCore.Platform.ControlPlane.Entities;
using Microsoft.EntityFrameworkCore;

namespace CommerceCore.Persistence;

public sealed class PlatformReadDbContext(
    DbContextOptions<PlatformReadDbContext> options)
    : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Storefront> Storefronts => Set<Storefront>();
    public DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new TenantConfiguration());
        modelBuilder.ApplyConfiguration(new StorefrontConfiguration());
        modelBuilder.ApplyConfiguration(new TenantMembershipConfiguration());

        base.OnModelCreating(modelBuilder);
    }
}
