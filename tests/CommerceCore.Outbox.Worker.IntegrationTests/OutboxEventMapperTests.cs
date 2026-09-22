using System.Text.Json;
using CommerceCore.Domain.Catalog.Products.Events;
using CommerceCore.Domain.Catalog.Products.ValueObjects;
using CommerceCore.Domain.Common.Events;
using CommerceCore.Outbox.Worker.Messaging;
using CommerceCore.Persistence.Outbox;
using CommerceCore.Platform.Contracts;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class OutboxEventMapperTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private static readonly DateTimeOffset OccurredOnUtc =
        new(2026, 9, 20, 10, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(false, "catalog.product.created.v1")]
    [InlineData(true, "catalog.product.archived.v1")]
    public void Map_PreservesContractAndRetryIdentity(
        bool archived,
        string expectedType)
    {
        ProductId productId = ProductId.New();
        LeasedOutboxMessage message = CreateMessage(productId, archived);

        OutboundEvent mapped = OutboxEventMapper.Map(message);

        Assert.Equal(message.Id, mapped.MessageId);
        Assert.Equal(message.TenantId.Value, mapped.TenantId);
        Assert.Equal(expectedType, mapped.EventType);

        using JsonDocument document = JsonDocument.Parse(mapped.Body);
        JsonElement root = document.RootElement;

        Assert.Equal(message.Id, root.GetProperty("messageId").GetGuid());
        Assert.Equal(
            message.TenantId.Value,
            root.GetProperty("tenantId").GetGuid());
        Assert.Equal(expectedType, root.GetProperty("type").GetString());
        Assert.Equal(productId.Value, root.GetProperty("productId").GetGuid());
        Assert.Equal(
            OccurredOnUtc,
            root.GetProperty("occurredOnUtc").GetDateTimeOffset());

        Assert.Equal(5, root.EnumerateObject().Count());

        OutboundEvent retry = OutboxEventMapper.Map(message with
        {
            LeaseId = Guid.NewGuid(),
            AttemptCount = 2
        });

        Assert.Equal(mapped.MessageId, retry.MessageId);
        Assert.Equal(mapped.Body.ToArray(), retry.Body.ToArray());
    }

    [Fact]
    public void Map_UnknownEventType_IsRejected()
    {
        LeasedOutboxMessage message = CreateMessage(ProductId.New()) with
        {
            Type = "Unknown.Event"
        };

        Assert.Throws<NotSupportedException>(
            () => OutboxEventMapper.Map(message));
    }

    [Fact]
    public void Map_MismatchedEventId_IsRejected()
    {
        LeasedOutboxMessage message = CreateMessage(ProductId.New()) with
        {
            Id = Guid.NewGuid()
        };

        Assert.Throws<JsonException>(
            () => OutboxEventMapper.Map(message));
    }

    [Fact]
    public void Map_MissingPayloadFields_IsRejected()
    {
        LeasedOutboxMessage message = CreateMessage(ProductId.New()) with
        {
            Content = "{}"
        };

        Assert.Throws<JsonException>(
            () => OutboxEventMapper.Map(message));
    }

    private static LeasedOutboxMessage CreateMessage(
        ProductId productId,
        bool archived = false)
    {
        IDomainEvent domainEvent = archived
            ? new ProductArchivedDomainEvent(
                productId,
                OccurredOnUtc,
                "test-user")
            : new ProductCreatedDomainEvent(productId, OccurredOnUtc);

        OutboxMessage stored = OutboxMessage.Create(
            domainEvent,
            TenantId.New(),
            JsonOptions);

        return new LeasedOutboxMessage(
            stored.Id,
            stored.TenantId,
            Guid.NewGuid(),
            1,
            stored.Type,
            stored.Content);
    }
}
