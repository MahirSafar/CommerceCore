using System.ComponentModel.DataAnnotations;

namespace CommerceCore.Outbox.Worker.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    [Required]
    public string HostName { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int Port { get; init; } = 5672;

    [Required]
    public string UserName { get; init; } = string.Empty;

    [Required]
    public string Password { get; init; } = string.Empty;

    [Required]
    public string VirtualHost { get; init; } = string.Empty;

    [Required]
    public string ExchangeName { get; init; } = string.Empty;

    public bool DispatcherEnabled { get; init; }
}