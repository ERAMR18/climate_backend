using Microsoft.Extensions.Configuration;
using RabbitMQ.Client;

namespace Climate.Contracts.Audit;

public sealed class RabbitMqAuditOptions
{
    public string Host { get; init; } = "";
    public int Port { get; init; } = 5672;
    public string User { get; init; } = "";
    public string Password { get; init; } = "";

    public static RabbitMqAuditOptions FromConfiguration(IConfiguration configuration)
    {
        string Required(string name) => !string.IsNullOrWhiteSpace(configuration[name])
            ? configuration[name]! : throw new InvalidOperationException($"{name} is required.");
        var port = configuration["RABBITMQ_PORT"] ?? "5672";
        if (!int.TryParse(port, out int number) || number is < 1 or > 65535)
            throw new InvalidOperationException("RABBITMQ_PORT must be between 1 and 65535.");
        return new() { Host = Required("RABBITMQ_HOST"), Port = number,
            User = Required("RABBITMQ_USER"), Password = Required("RABBITMQ_PASSWORD") };
    }

    public ConnectionFactory CreateFactory() => new()
    {
        HostName = Host, Port = Port, UserName = User, Password = Password,
        AutomaticRecoveryEnabled = false, RequestedConnectionTimeout = TimeSpan.FromSeconds(5),
        ClientProvidedName = $"climate-audit:{Environment.MachineName}"
    };
}
