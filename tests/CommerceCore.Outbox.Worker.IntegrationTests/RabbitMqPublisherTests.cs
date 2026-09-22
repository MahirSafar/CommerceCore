using System.Text;
using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.Messaging;
using CommerceCore.Outbox.Worker.RabbitMq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Testcontainers.RabbitMq;

namespace CommerceCore.Outbox.Worker.IntegrationTests;

public sealed class RabbitMqFixture : IAsyncLifetime
{
    private readonly RabbitMqContainer _container = new RabbitMqBuilder("rabbitmq:4.3-management").Build();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    public Task<IConnection> ConnectAsync(
        CancellationToken cancellationToken)
    {
        return CreateFactory().CreateConnectionAsync(cancellationToken);
    }

    public RabbitMqOptions CreateOptions(string exchangeName)
    {
        ConnectionFactory factory = CreateFactory();

        return new RabbitMqOptions
        {
            HostName = factory.HostName,
            Port = factory.Port,
            UserName = factory.UserName,
            Password = factory.Password,
            VirtualHost = factory.VirtualHost,
            ExchangeName = exchangeName
        };
    }

    public async Task<RabbitMqOptions> CreateRestrictedPublisherAsync(
        string exchangeName,
        CancellationToken cancellationToken)
    {
        string userName = $"publisher_{Guid.NewGuid():N}";
        string password = Guid.NewGuid().ToString("N");
        RabbitMqOptions endpoint = CreateOptions(exchangeName);

        await RunControlCommandAsync(
            ["add_user", userName, password],
            cancellationToken);

        await RunControlCommandAsync(
            [
                "set_permissions",
                "-p",
                endpoint.VirtualHost,
                userName,
                "^$",
                $"^{System.Text.RegularExpressions.Regex.Escape(exchangeName)}$",
                "^$"
            ],
            cancellationToken);

        await RunControlCommandAsync(
            [
                "set_topic_permissions",
                "-p",
                endpoint.VirtualHost,
                userName,
                exchangeName,
                @"^catalog\.product\.(created|archived)\.v1$",
                "^$"
            ],
            cancellationToken);

        return new RabbitMqOptions
        {
            HostName = endpoint.HostName,
            Port = endpoint.Port,
            UserName = userName,
            Password = password,
            VirtualHost = endpoint.VirtualHost,
            ExchangeName = exchangeName
        };
    }

    private async Task RunControlCommandAsync(
        string[] arguments,
        CancellationToken cancellationToken)
    {
        var result = await _container.ExecAsync(
            ["rabbitmqctl", .. arguments],
            cancellationToken);

        Assert.True(result.ExitCode == 0, result.Stderr);
    }

    private ConnectionFactory CreateFactory() => new()
    {
        Uri = new Uri(_container.GetConnectionString())
    };
}

public sealed class RabbitMqPublisherTests(RabbitMqFixture fixture) : IClassFixture<RabbitMqFixture>
{
    private const string EventType = "catalog.product.created.v1";

    [Theory]
    [InlineData(1)]
    [InlineData(8)]
    public async Task Publish_RoutedMessages_PreservesBodyAndMetadata(
        int messageCount)
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string exchangeName = $"test.events.{Guid.NewGuid():N}";
        string queueName = $"test.queue.{Guid.NewGuid():N}";

        await using IConnection connection = await fixture.ConnectAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: new Dictionary<string, object?>
            {
                ["x-queue-type"] = "quorum"
            },
            cancellationToken: cancellationToken);

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: exchangeName,
            routingKey: EventType,
            cancellationToken: cancellationToken);

        await using var publisher = await RabbitMqEventPublisher.CreateAsync(
            fixture.CreateOptions(exchangeName),
            cancellationToken);

        Guid tenantId = Guid.NewGuid();

        OutboundEvent[] messages = Enumerable.Range(0, messageCount)
            .Select(index => new OutboundEvent(
                Guid.NewGuid(),
                tenantId,
                EventType,
                Encoding.UTF8.GetBytes($"{{\"sequence\":{index}}}")))
            .ToArray();

        await Task.WhenAll(messages.Select(message => publisher.PublishAsync(message, cancellationToken)));

        var expected = messages.ToDictionary(
            message => message.MessageId.ToString("D"),
            StringComparer.Ordinal);

        for (int index = 0; index < messageCount; index++)
        {
            BasicGetResult? received = await channel.BasicGetAsync(
                queue: queueName,
                autoAck: true,
                cancellationToken: cancellationToken);

            Assert.NotNull(received);
            Assert.NotNull(received.BasicProperties.MessageId);
            Assert.True(expected.Remove(
                received.BasicProperties.MessageId,
                out OutboundEvent? sent));
            Assert.NotNull(sent);

            Assert.Equal(
                sent.Body.ToArray(),
                received.Body.ToArray());
            Assert.Equal(EventType, received.RoutingKey);
            Assert.Equal(EventType, received.BasicProperties.Type);
            Assert.Equal("commercecore", received.BasicProperties.AppId);
            Assert.Equal("application/json", received.BasicProperties.ContentType);
            Assert.Equal("utf-8", received.BasicProperties.ContentEncoding);
            Assert.True(received.BasicProperties.Persistent);
            Assert.NotNull(received.BasicProperties.Headers);

            byte[] tenantHeader = Assert.IsType<byte[]>(
                received.BasicProperties.Headers["tenant-id"]);
            Assert.Equal(
                tenantId.ToString("D"),
                Encoding.UTF8.GetString(tenantHeader));
        }

        Assert.Empty(expected);

        Assert.Null(await channel.BasicGetAsync(
            queue: queueName,
            autoAck: true,
            cancellationToken: cancellationToken));
    }

    [Fact]
    public async Task Publish_WithoutBinding_ThrowsReturnedMessageException()
    {
        CancellationToken cancellationToken = TestContext.Current.CancellationToken;

        string exchangeName = $"test.unroutable.{Guid.NewGuid():N}";

        await using IConnection connection = await fixture.ConnectAsync(cancellationToken);
        await using IChannel channel = await connection.CreateChannelAsync(
            cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: exchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        await using var publisher = await RabbitMqEventPublisher.CreateAsync(
            fixture.CreateOptions(exchangeName),
            cancellationToken);

        var message = new OutboundEvent(
            Guid.NewGuid(),
            Guid.NewGuid(),
            EventType,
            Encoding.UTF8.GetBytes("{}"));

        PublishException exception = await Assert.ThrowsAnyAsync<PublishException>(
            () => publisher.PublishAsync(
                message,
                cancellationToken));

        Assert.True(exception.IsReturn);
    }
}
