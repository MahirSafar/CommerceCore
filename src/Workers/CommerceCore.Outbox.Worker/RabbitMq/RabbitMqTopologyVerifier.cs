using CommerceCore.Outbox.Worker.Configuration;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace CommerceCore.Outbox.Worker.RabbitMq;

public sealed partial class RabbitMqTopologyVerifier(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqTopologyVerifier> logger)
    : IHostedService
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqTopologyVerifier> _logger = logger;

    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Information,
        Message = "Verified RabbitMQ exchange {ExchangeName} exists.")]
    private static partial void LogExchangeVerified(
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

        await channel.ExchangeDeclarePassiveAsync(
            exchange: _options.ExchangeName,
            cancellationToken: cancellationToken);

        LogExchangeVerified(_logger, _options.ExchangeName);
    }

    public Task StopAsync(CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
