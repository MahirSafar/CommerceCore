using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Domain.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CommerceCore.Persistence.Interceptors;

public sealed class AuditingSaveChangesInterceptor(
    IClock clock,
    ICurrentUser currentUser)
    : SaveChangesInterceptor
{
    private readonly IClock _clock = clock;
    private readonly ICurrentUser _currentUser = currentUser;

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
            return;

        context.ChangeTracker.DetectChanges();

        DateTimeOffset nowUtc = _clock.UtcNow.ToUniversalTime();
        string? userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries()
                     .Where(entry =>
                         entry.Entity is IAuditableEntity &&
                         RequiresAuditUpdate(entry)))
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc))
                    .CurrentValue = nowUtc;

                entry.Property(nameof(IAuditableEntity.CreatedBy))
                    .CurrentValue = userId;

                continue;
            }

            entry.Property(nameof(IAuditableEntity.UpdatedAtUtc))
                .CurrentValue = nowUtc;

            entry.Property(nameof(IAuditableEntity.UpdatedBy))
                .CurrentValue = userId;
        }
    }

    private static bool RequiresAuditUpdate(EntityEntry entry)
    {
        if (entry.State is EntityState.Added or EntityState.Modified)
            return true;

        return entry.References.Any(reference =>
            reference.TargetEntry is { } targetEntry &&
            targetEntry.Metadata.IsOwned() &&
            targetEntry.State is EntityState.Added
                or EntityState.Modified
                or EntityState.Deleted);
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditValues(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditValues(eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }
}