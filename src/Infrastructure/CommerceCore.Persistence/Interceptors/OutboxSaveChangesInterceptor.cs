using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using System.Text.Json;

namespace CommerceCore.Persistence.Interceptors;

public sealed class OutboxSaveChangesInterceptor(ITenantContext? tenantContext = null) : SaveChangesInterceptor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ITenantContext? _tenantContext = tenantContext;

    private void AddOutboxMessages(DbContext? dbContext)
    {
        if (dbContext is null)
            return;

        dbContext.ChangeTracker.DetectChanges();

        IHasDomainEvents[] agregates = [.. dbContext.ChangeTracker
            .Entries<IHasDomainEvents>()
            .Select(entry => entry.Entity)];

        IDomainEvent[] domainEvents = [.. agregates.SelectMany(agregates => agregates.DomainEvents)];

        if (domainEvents.Length == 0)
            return;

        HashSet<Guid> existingOutboxMessageIds = [.. dbContext.ChangeTracker
               .Entries<OutboxMessage>()
               .Where(entry => entry.State != EntityState.Detached)
               .Select(entry => entry.Entity.Id)];

        Guid currentTenantId = _tenantContext?.TenantId?.Value ?? Guid.Empty;

        foreach (var aggregate in agregates)
        {
            Guid aggregateTenantId = currentTenantId;
            if (aggregateTenantId == Guid.Empty)
            {
                if (aggregate is Product product) aggregateTenantId = product.TenantId.Value;
                else if (aggregate is ProductType productType) aggregateTenantId = productType.TenantId.Value;
            }

            foreach (var domainEvent in aggregate.DomainEvents)
            {
                if (!existingOutboxMessageIds.Add(domainEvent.EventId))
                    continue;

                dbContext.Set<OutboxMessage>().Add(
                    OutboxMessage.Create(
                        domainEvent,
                        aggregateTenantId,
                        JsonOptions));
            }
        }
    }

    private static void ClearDomainEvents(DbContext? dbContext)
    {
        if (dbContext is null)
            return;

        foreach (var aggregate in dbContext.ChangeTracker.Entries<IHasDomainEvents>().Select(entry => entry.Entity))
            aggregate.ClearDomainEvents();


    }
    public override ValueTask<int> SavedChangesAsync(
       SaveChangesCompletedEventData eventData,
       int result,
       CancellationToken cancellationToken = default)
    {
        ClearDomainEvents(eventData.Context);

        return base.SavedChangesAsync(
            eventData,
            result,
            cancellationToken);
    }
    public override int SavedChanges(
       SaveChangesCompletedEventData eventData,
       int result)
    {
        ClearDomainEvents(eventData.Context);

        return base.SavedChanges(eventData, result);
    }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
    DbContextEventData eventData,
    InterceptionResult<int> result,
    CancellationToken cancellationToken = default)
    {
        AddOutboxMessages(eventData.Context);

        return base.SavingChangesAsync(
            eventData,
            result,
            cancellationToken);
    }
    public override InterceptionResult<int> SavingChanges(
           DbContextEventData eventData,
           InterceptionResult<int> result)
    {
        AddOutboxMessages(eventData.Context);

        return base.SavingChanges(eventData, result);
    }
}
