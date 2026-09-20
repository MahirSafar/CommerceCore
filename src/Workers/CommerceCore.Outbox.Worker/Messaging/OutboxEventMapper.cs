using System.Text.Json;
using CommerceCore.Persistence.Outbox;

namespace CommerceCore.Outbox.Worker.Messaging;

public static class OutboxEventMapper
{
    // Persist edilmiş adlardır: domain namespace dəyişsə də saxlanılmalıdır.
    private const string ProductCreatedSource =
        "CommerceCore.Domain.Catalog.Products.Events.ProductCreatedDomainEvent";

    private const string ProductArchivedSource =
        "CommerceCore.Domain.Catalog.Products.Events.ProductArchivedDomainEvent";

    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    public static OutboundEvent Map(LeasedOutboxMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);

        string eventType = message.Type switch
        {
            ProductCreatedSource => "catalog.product.created.v1",
            ProductArchivedSource => "catalog.product.archived.v1",
            _ => throw new NotSupportedException(
                "The outbox event type has no integration contract.")
        };

        using JsonDocument document = JsonDocument.Parse(message.Content);
        JsonElement root = document.RootElement;

        Guid eventId = ReadGuid(root, "eventId");

        if (eventId != message.Id)
        {
            throw new JsonException(
                "Payload event ID does not match the outbox message ID.");
        }

        if (message.TenantId.Value == Guid.Empty)
        {
            throw new JsonException("Outbox tenant ID must not be empty.");
        }

        if (!root.TryGetProperty("productId", out JsonElement product))
        {
            throw new JsonException("Product ID is required.");
        }

        Guid productId = ReadGuid(product, "value");

        if (!root.TryGetProperty("occurredOnUtc", out JsonElement occurred) ||
            occurred.ValueKind != JsonValueKind.String ||
            !occurred.TryGetDateTimeOffset(out DateTimeOffset occurredOnUtc) ||
            occurredOnUtc == default)
        {
            throw new JsonException("A valid event timestamp is required.");
        }

        var contract = new ProductLifecycleEventV1(
            message.Id,
            message.TenantId.Value,
            eventType,
            occurredOnUtc.ToUniversalTime(),
            productId);

        return new OutboundEvent(
            message.Id,
            message.TenantId.Value,
            eventType,
            JsonSerializer.SerializeToUtf8Bytes(contract, JsonOptions));
    }

    private static Guid ReadGuid(JsonElement owner, string propertyName)
    {
        if (owner.ValueKind != JsonValueKind.Object ||
            !owner.TryGetProperty(propertyName, out JsonElement property) ||
            property.ValueKind != JsonValueKind.String ||
            !property.TryGetGuid(out Guid value) ||
            value == Guid.Empty)
        {
            throw new JsonException(
                $"A non-empty GUID is required for '{propertyName}'.");
        }

        return value;
    }

    private sealed record ProductLifecycleEventV1(
        Guid MessageId,
        Guid TenantId,
        string Type,
        DateTimeOffset OccurredOnUtc,
        Guid ProductId);
}
