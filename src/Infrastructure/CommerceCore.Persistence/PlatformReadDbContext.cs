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

    private static NotSupportedException CreateReadOnlyException() =>
        new("PlatformReadDbContext is read-only.");

    public override int SaveChanges() =>
        throw CreateReadOnlyException();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw CreateReadOnlyException();

    public override Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default) =>
        Task.FromException<int>(CreateReadOnlyException());

    public override Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default) =>
        Task.FromException<int>(CreateReadOnlyException());
}
