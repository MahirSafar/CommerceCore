using System.Text;
using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.Messaging;
using CommerceCore.Outbox.Worker.RabbitMq;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class RabbitMqPublisherPermissionTests(RabbitMqFixture fixture) : IClassFixture<RabbitMqFixture>
{
    [Theory]
    [InlineData("catalog.product.created.v1")]
    [InlineData("catalog.product.archived.v1")]
    public async Task RestrictedPublisher_CanVerifyExchange_AndPublishAllowedEvent(
        string eventType)
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        string exchangeName = $"permissions.events.{Guid.NewGuid():N}";

        await using IConnection admin = await fixture.ConnectAsync(token);
        await using IChannel adminChannel = await admin.CreateChannelAsync(
            cancellationToken: token);

        string queueName = await CreateTopologyAsync(
            adminChannel,
            exchangeName,
            token);

        RabbitMqOptions options = await fixture.CreateRestrictedPublisherAsync(
            exchangeName,
            token);

        var verifier = new RabbitMqTopologyVerifier(
            Options.Create(options),
            NullLogger<RabbitMqTopologyVerifier>.Instance);

        // Configure permission olmadan production verifier işləməlidir.
        await verifier.StartAsync(token);

        await using var publisher = await RabbitMqEventPublisher.CreateAsync(
            options,
            token);

        OutboundEvent message = CreateMessage(eventType);

        await publisher.PublishAsync(message, token);

        BasicGetResult? received = await adminChannel.BasicGetAsync(
            queue: queueName,
            autoAck: true,
            cancellationToken: token);

        Assert.NotNull(received);
        Assert.Equal(message.MessageId.ToString("D"), received.BasicProperties.MessageId);
        Assert.Equal(eventType, received.RoutingKey);
        Assert.Equal(message.Body.ToArray(), received.Body.ToArray());
    }

    [Fact]
    public async Task RestrictedPublisher_CannotPublishForbiddenRoutingKey()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        string exchangeName = $"permissions.events.{Guid.NewGuid():N}";

        await using IConnection admin = await fixture.ConnectAsync(token);
        await using IChannel adminChannel = await admin.CreateChannelAsync(
            cancellationToken: token);

        string queueName = await CreateTopologyAsync(
            adminChannel,
            exchangeName,
            token);

        RabbitMqOptions options = await fixture.CreateRestrictedPublisherAsync(
            exchangeName,
            token);

        await using var publisher = await RabbitMqEventPublisher.CreateAsync(
            options,
            token);

        OutboundEvent message = CreateMessage("catalog.product.deleted.v1");

        OperationInterruptedException exception = await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            () => publisher.PublishAsync(message, token));

        Assert.NotNull(exception.ShutdownReason);
        Assert.Equal((ushort)403, exception.ShutdownReason.ReplyCode);

        Assert.Null(await adminChannel.BasicGetAsync(
            queue: queueName,
            autoAck: true,
            cancellationToken: token));
    }

    [Fact]
    public async Task RestrictedPublisher_CannotReadQueue_AndMessageRemainsAvailable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        string exchangeName = $"permissions.events.{Guid.NewGuid():N}";

        await using IConnection admin = await fixture.ConnectAsync(token);
        await using IChannel adminChannel = await admin.CreateChannelAsync(
            cancellationToken: token);

        string queueName = await CreateTopologyAsync(
            adminChannel,
            exchangeName,
            token);

        RabbitMqOptions options = await fixture.CreateRestrictedPublisherAsync(
            exchangeName,
            token);

        OutboundEvent message = CreateMessage("catalog.product.created.v1");

        await using (var publisher = await RabbitMqEventPublisher.CreateAsync(
            options,
            token))
        {
            await publisher.PublishAsync(message, token);
        }

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = false
        };

        await using IConnection restricted = await factory.CreateConnectionAsync(token);
        await using IChannel restrictedChannel = await restricted.CreateChannelAsync(
            cancellationToken: token);

        OperationInterruptedException exception = await Assert.ThrowsAnyAsync<OperationInterruptedException>(
            async () =>
            {
                await restrictedChannel.BasicGetAsync(
                    queue: queueName,
                    autoAck: true,
                    cancellationToken: token);
            });

        Assert.NotNull(exception.ShutdownReason);
        Assert.Equal((ushort)403, exception.ShutdownReason.ReplyCode);

        // Qadağan oxuma mesajı queue-dan çıxarmamalıdır.
        BasicGetResult? received = await adminChannel.BasicGetAsync(
            queue: queueName,
            autoAck: true,
            cancellationToken: token);

        Assert.NotNull(received);
        Assert.Equal(message.MessageId.ToString("D"), received.BasicProperties.MessageId);
    }

    private static OutboundEvent CreateMessage(string eventType) => new(
        Guid.NewGuid(),
        Guid.NewGuid(),
        eventType,
        Encoding.UTF8.GetBytes("{}"));

    private static async Task<string> CreateTopologyAsync(
        IChannel adminChannel,
        string exchangeName,
        CancellationToken cancellationToken)
    {
        await adminChannel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        string queueName = $"permissions.queue.{Guid.NewGuid():N}";

        await adminChannel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            },
            cancellationToken: cancellationToken);

        // Qadağan event də route oluna bilsin:
        // test unroutable mesajı deyil, ACL rəddini yoxlayır.
        await adminChannel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: "catalog.product.#",
            cancellationToken: cancellationToken);

        return queueName;
    }
}
