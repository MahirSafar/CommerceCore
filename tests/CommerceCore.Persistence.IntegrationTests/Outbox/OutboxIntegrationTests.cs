using CommerceCore.Domain.Catalog.Products;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.ProductTypes;
using CommerceCore.Domain.Catalog.ProductTypes.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects;
using CommerceCore.Domain.Common.ValueObjects.Localization;
using CommerceCore.Persistence.IntegrationTests.Infrastructure;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace CommerceCore.Persistence.IntegrationTests.Outbox;

[Collection(nameof(PostgreSqlCollection))]
public sealed class OutboxIntegrationTests(
    PostgreSqlFixture fixture)
{
    [Fact]
    public async Task SaveChanges_WhenProductIsCreated_WritesOutboxMessage()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        TenantId tenantId = await fixture.CreateTenantAsync(cancellationToken);
        var tenantContext = fixture.Services.GetRequiredService<TestTenantContext>();
        tenantContext.SetTenant(tenantId);

        await using var scope = fixture.Services.CreateAsyncScope();

        var dbContext = scope.ServiceProvider
            .GetRequiredService<CommerceCoreDbContext>();

        var productType = ProductType.CreateRoot(
            tenantId,
            ProductTypeCode.Create($"type_{Guid.NewGuid():N}"[..12]),
            isAssignable: true);

        dbContext.ProductTypes.Add(productType);
        await dbContext.SaveChangesAsync(cancellationToken);

        var language = LanguageCode.Create("en");

        var name = LocalizedText.Create(
            language,
            [
                new KeyValuePair<LanguageCode, string>(
                    language,
                    "Integration test product")
            ]);

        var product = Product.Create(
            tenantId,
            name,
            Money.Create(99.99m, "USD"),
            productType.Id,
            new DateTimeOffset(
                2026, 8, 15, 15, 0, 0, TimeSpan.Zero));

        var createdEvent = Assert.IsType<ProductCreatedDomainEvent>(
            Assert.Single(product.DomainEvents));

        dbContext.Products.Add(product);

        await dbContext.SaveChangesAsync(cancellationToken);

        Assert.Equal("integration-test", product.CreatedBy);
        Assert.NotEqual(default, product.CreatedAtUtc);
        Assert.Null(product.UpdatedAtUtc);
        Assert.Empty(product.DomainEvents);

        dbContext.ChangeTracker.Clear();

        var outboxMessage = await dbContext
            .Set<OutboxMessage>()
            .SingleAsync(message =>
                message.Id == createdEvent.EventId, cancellationToken);

        Assert.Equal(
            typeof(ProductCreatedDomainEvent).FullName,
            outboxMessage.Type);

        Assert.Equal(
            createdEvent.OccurredOnUtc,
            outboxMessage.OccurredOnUtc);

        Assert.Null(outboxMessage.ProcessedOnUtc);
        Assert.Equal(0, outboxMessage.AttemptCount);

        using var document = JsonDocument.Parse(
            outboxMessage.Content);

        var root = document.RootElement;

        Assert.Equal(
            createdEvent.EventId,
            root.GetProperty("eventId").GetGuid());

        Assert.Equal(
            product.Id.Value,
            root.GetProperty("productId")
                .GetProperty("value")
                .GetGuid());
    }
}