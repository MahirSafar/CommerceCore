using CommerceCore.Outbox.Worker.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommerceCore.Outbox.Worker.RabbitMq;

public sealed partial class RabbitMqTopologyInitializer(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyInitializer> logger)
    : IHostedService
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqTopologyInitializer> _logger = logger;

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Declared RabbitMQ topic exchange {ExchangeName}.")]
    private static partial void LogExchangeDeclared(
        ILogger logger,
        string exchangeName);

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        ConnectionFactory factory = new()
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost
        };

        await using IConnection connection =
            await factory.CreateConnectionAsync(cancellationToken);

        await using IChannel channel =
            await connection.CreateChannelAsync(
                cancellationToken: cancellationToken);

        await channel.ExchangeDeclareAsync(
            exchange: _options.ExchangeName,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        LogExchangeDeclared(_logger, _options.ExchangeName);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
