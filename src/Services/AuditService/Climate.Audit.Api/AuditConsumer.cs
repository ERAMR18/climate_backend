using System.Text.Json;
using Climate.Audit.Application.Auditing;
using Climate.Contracts.Audit;
using RabbitMQ.Client;

namespace Climate.Audit.Api;

public sealed partial class AuditConsumer(RabbitMqAuditOptions options, IServiceScopeFactory scopes, ILogger<AuditConsumer> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = await options.CreateFactory().CreateConnectionAsync(stoppingToken);
                await using var channel = await connection.CreateChannelAsync(cancellationToken: stoppingToken);
                await AuditTopology.DeclareAsync(channel, stoppingToken);
                while (!stoppingToken.IsCancellationRequested)
                {
                    var delivery = await channel.BasicGetAsync(AuditTopology.Queue, autoAck: false, stoppingToken);
                    if (delivery is null)
                    {
                        await Task.Delay(250, stoppingToken);
                        continue;
                    }
                    bool saved = await PersistAsync(delivery.Body.ToArray(), stoppingToken);
                    if (saved) await channel.BasicAckAsync(delivery.DeliveryTag, multiple: false, stoppingToken);
                    else await channel.BasicNackAsync(delivery.DeliveryTag, multiple: false, requeue: false, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception)
            {
                Disconnected(logger, exception);
                try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            }
        }
    }

    public async Task<bool> PersistAsync(byte[] body, CancellationToken token)
    {
        AuditLogRequested? message;
        try { message = JsonSerializer.Deserialize<AuditLogRequested>(body); }
        catch (JsonException exception)
        {
            Malformed(logger, exception);
            return false;
        }
        if (message is null) return false;
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                // Fresh context per attempt; a duplicate after a lost ACK uses the existing primary key.
                await using var scope = scopes.CreateAsyncScope();
                var service = scope.ServiceProvider.GetRequiredService<IAuditService>();
                var result = await service.RecordAsync(new(message.EventId, message.UserId, message.UserName,
                    message.Action, message.Resource, message.ResourceId, message.Description,
                    message.IpAddress, message.OccurredAt, message.CorrelationId), token);
                if (!result.IsSuccess) Invalid(logger, message.EventId);
                return result.IsSuccess;
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception exception)
            {
                PersistenceFailed(logger, attempt, message.EventId, exception);
                if (attempt < 3) await Task.Delay(TimeSpan.FromSeconds(attempt), token);
            }
        }
        Exhausted(logger, message.EventId);
        return false;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit consumer disconnected; reconnecting in 5 seconds")]
    private static partial void Disconnected(ILogger logger, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Malformed audit event; sending to DLQ")]
    private static partial void Malformed(ILogger logger, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Invalid audit event {EventId}; sending to DLQ")]
    private static partial void Invalid(ILogger logger, Guid eventId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit persistence attempt {Attempt}/3 failed for {EventId}")]
    private static partial void PersistenceFailed(ILogger logger, int attempt, Guid eventId, Exception exception);
    [LoggerMessage(Level = LogLevel.Error, Message = "Audit event {EventId} exhausted retries; sending to DLQ")]
    private static partial void Exhausted(ILogger logger, Guid eventId);
}
