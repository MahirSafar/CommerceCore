using System.Text;
using CommerceCore.Outbox.Worker.Configuration;
using CommerceCore.Outbox.Worker.Messaging;
using RabbitMQ.Client;

namespace CommerceCore.Outbox.Worker.RabbitMq;

public sealed class RabbitMqEventPublisher : IAsyncDisposable
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromSeconds(10);
    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly string _exchangeName;
    private readonly SemaphoreSlim _publishLock = new(1, 1);
    private bool _disposed;

    private RabbitMqEventPublisher(
        IConnection connection,
        IChannel channel,
        string exchangeName)
    {
        _connection = connection;
        _channel = channel;
        _exchangeName = exchangeName;
    }

    public static async Task<RabbitMqEventPublisher> CreateAsync(
        RabbitMqOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(options);

        var factory = new ConnectionFactory
        {
            HostName = options.HostName,
            Port = options.Port,
            UserName = options.UserName,
            Password = options.Password,
            VirtualHost = options.VirtualHost,
            AutomaticRecoveryEnabled = true
        };

        IConnection connection = await factory.CreateConnectionAsync(cancellationToken);

        try
        {
            var channelOptions = new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true);

            IChannel channel = await connection.CreateChannelAsync(
                channelOptions,
                cancellationToken);

            return new RabbitMqEventPublisher(
                connection,
                channel,
                options.ExchangeName);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }
    }

    public async Task PublishAsync(
        OutboundEvent message,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentException.ThrowIfNullOrWhiteSpace(message.EventType);

        if (message.MessageId == Guid.Empty)
        {
            throw new ArgumentException(
                "Message ID must not be empty.",
                nameof(message));
        }

        if (message.TenantId == Guid.Empty)
        {
            throw new ArgumentException(
                "Tenant ID must not be empty.",
                nameof(message));
        }

        if (Encoding.UTF8.GetByteCount(message.EventType) > 255)
        {
            throw new ArgumentException(
                "Event type must not exceed 255 UTF-8 bytes.",
                nameof(message));
        }

        if (message.Body.IsEmpty)
        {
            throw new ArgumentException(
                "Message body must not be empty.",
                nameof(message));
        }

        ObjectDisposedException.ThrowIf(_disposed, this);

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        timeout.CancelAfter(PublishTimeout);

        await _publishLock.WaitAsync(timeout.Token);

        try
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            var properties = new BasicProperties
            {
                MessageId = message.MessageId.ToString("D"),
                Type = message.EventType,
                AppId = "commercecore",
                ContentType = "application/json",
                ContentEncoding = "utf-8",
                Persistent = true,
                Headers = new Dictionary<string, object?>
                {
                    ["tenant-id"] = message.TenantId.ToString("D")
                }
            };

            await _channel.BasicPublishAsync(
                exchange: _exchangeName,
                routingKey: message.EventType,
                mandatory: true,
                basicProperties: properties,
                body: message.Body,
                cancellationToken: timeout.Token);
        }
        finally
        {
            _publishLock.Release();
        }
    }

    // Owner bütün PublishAsync çağırışlarını bitirdikdən sonra çağırmalıdır.
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        try
        {
            await _channel.DisposeAsync();
        }
        finally
        {
            try
            {
                await _connection.DisposeAsync();
            }
            finally
            {
                _publishLock.Dispose();
            }
        }
    }
}
