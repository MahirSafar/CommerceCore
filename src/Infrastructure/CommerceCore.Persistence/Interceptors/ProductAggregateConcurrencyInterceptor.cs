using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CommerceCore.Persistence.Interceptors;

public sealed class ProductAggregateConcurrencyInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        PrepareAggregateWrites(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        PrepareAggregateWrites(eventData.Context);
        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }

    private static void PrepareAggregateWrites(DbContext? context)
    {
        if (context is null)
            return;

        context.ChangeTracker.DetectChanges();

        EntityEntry<ProductVariant>[] changedVariants = context.ChangeTracker
            .Entries<ProductVariant>()
            .Where(HasVariantChanges)
            .ToArray();

        if (changedVariants.Length == 0)
            return;

        var products = context.ChangeTracker
            .Entries<Product>()
            .ToDictionary(
                entry => (entry.Entity.TenantId, entry.Entity.Id));

        foreach (EntityEntry<ProductVariant> variantEntry in changedVariants)
        {
            ProductId productId = variantEntry
                .Property<ProductId>("ProductId")
                .CurrentValue;

            if (!products.TryGetValue(
                (variantEntry.Entity.TenantId, productId),
                out EntityEntry<Product>? productEntry))
            {
                throw new InvalidOperationException(
                    "Product variant changes require the owning Product " +
                    "to be tracked in the same DbContext.");
            }

            if (productEntry.State is EntityState.Added or EntityState.Deleted)
                continue;

            if (productEntry.State == EntityState.Unchanged)
            {
                // Preserve the status value, but require a root-row UPDATE.
                // Its existing xmin token becomes the aggregate write guard.
                productEntry.Property(product => product.Status)
                    .IsModified = true;
            }
        }
    }

    private static bool HasVariantChanges(
        EntityEntry<ProductVariant> entry)
    {
        if (IsWriteState(entry.State))
            return true;

        // Price is owned: a price change may leave the variant entry unchanged.
        EntityEntry? priceEntry = entry
            .Reference(variant => variant.Price)
            .TargetEntry;

        return priceEntry is not null && IsWriteState(priceEntry.State);
    }

    private static bool IsWriteState(EntityState state) =>
        state is EntityState.Added or EntityState.Modified or EntityState.Deleted;
}
