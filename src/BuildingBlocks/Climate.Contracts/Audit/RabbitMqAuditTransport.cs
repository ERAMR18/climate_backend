using System.Text.Json;
using RabbitMQ.Client;

namespace Climate.Contracts.Audit;

public interface IAuditEventTransport
{
    Task SendAsync(AuditLogRequested message, CancellationToken token);
}

// One worker owns this channel; concurrent channel publishing is deliberately avoided.
public sealed class RabbitMqAuditTransport(RabbitMqAuditOptions options) : IAuditEventTransport, IAsyncDisposable
{
    private IConnection? connection;
    private IChannel? channel;

    public async Task SendAsync(AuditLogRequested message, CancellationToken token)
    {
        try
        {
            if (channel is null || !channel.IsOpen)
            {
                await DisposeAsync();
                connection = await options.CreateFactory().CreateConnectionAsync(token);
                channel = await connection.CreateChannelAsync(new CreateChannelOptions(
                    publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true), token);
                await AuditTopology.DeclareAsync(channel, token);
            }
            var properties = new BasicProperties
            {
                Persistent = true, ContentType = "application/json", MessageId = message.EventId.ToString(),
                CorrelationId = message.CorrelationId, Type = nameof(AuditLogRequested)
            };
            await channel.BasicPublishAsync(AuditTopology.Exchange, AuditTopology.RoutingKey,
                mandatory: true, basicProperties: properties, body: JsonSerializer.SerializeToUtf8Bytes(message), cancellationToken: token);
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (channel is not null) { await channel.DisposeAsync(); channel = null; }
        if (connection is not null) { await connection.DisposeAsync(); connection = null; }
    }
}
