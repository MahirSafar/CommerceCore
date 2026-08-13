using CommerceCore.Application.Common.Abstractions;
using CommerceCore.Domain.Common.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CommerceCore.Persistence.Interceptors;

public sealed class AuditingSaveChangesInterceptor(IClock clock, ICurrentUser currentUser) : SaveChangesInterceptor
{
    private readonly IClock _clock = clock;
    private readonly ICurrentUser _currentUser = currentUser;

    private void ApplyAuditValues(DbContext? context)
    {
        if (context is null)
            return;

        DateTimeOffset nowUtc = _clock.UtcNow.ToUniversalTime();
        string? userId = _currentUser.UserId;

        foreach (var entry in context.ChangeTracker.Entries().Where(entry => entry.Entity is IAuditableEntity && entry.State is EntityState.Added or EntityState.Modified))
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditableEntity.CreatedAtUtc))
                    .CurrentValue = nowUtc;

                entry.Property(nameof(IAuditableEntity.CreatedBy))
                    .CurrentValue = userId;
            }

            entry.Property(nameof(IAuditableEntity.UpdatedAtUtc))
                .CurrentValue = nowUtc;

            entry.Property(nameof(IAuditableEntity.UpdatedBy))
                .CurrentValue = userId;
        }
    }
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
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
