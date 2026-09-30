using RabbitMQ.Client;

namespace Climate.Contracts.Audit;

public static class AuditTopology
{
    public const string Exchange = "climate.audit";
    public const string Queue = "climate.audit.events";
    public const string RoutingKey = "audit.created";
    public const string DeadLetterQueue = "climate.audit.events.dlq";
    public const string DeadLetterExchange = "climate.audit.dead";

    public static async Task DeclareAsync(IChannel channel, CancellationToken token)
    {
        await channel.ExchangeDeclareAsync(Exchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: token);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: token);
        await channel.QueueDeclareAsync(DeadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: token);
        await channel.QueueBindAsync(DeadLetterQueue, DeadLetterExchange, RoutingKey, cancellationToken: token);
        await channel.QueueDeclareAsync(Queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?> { ["x-dead-letter-exchange"] = DeadLetterExchange,
                ["x-dead-letter-routing-key"] = RoutingKey }, cancellationToken: token);
        await channel.QueueBindAsync(Queue, Exchange, RoutingKey, cancellationToken: token);
    }
}
