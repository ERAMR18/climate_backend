using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Climate.Contracts.Audit;

public sealed partial class BackgroundAuditPublisher(IAuditEventTransport transport, ILogger<BackgroundAuditPublisher> logger)
    : BackgroundService, IAuditEventPublisher
{
    private readonly Channel<AuditLogRequested> pending = Channel.CreateBounded<AuditLogRequested>(
        new BoundedChannelOptions(10000) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });

    public Task PublishAsync(AuditLogRequested auditEvent, CancellationToken cancellationToken)
    {
        // Business data has already committed. Request cancellation must not discard its audit.
        if (!pending.Writer.TryWrite(auditEvent))
            BufferFull(logger, auditEvent.EventId, auditEvent.Action, auditEvent.ResourceId);
        return Task.CompletedTask;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var message in pending.Reader.ReadAllAsync(stoppingToken))
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                        timeout.CancelAfter(TimeSpan.FromSeconds(15));
                        await transport.SendAsync(message, timeout.Token);
                        break;
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { throw; }
                    catch (Exception exception)
                    {
                        PublishFailed(logger, message.EventId, exception);
                        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
                    }
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }

    public override Task StopAsync(CancellationToken cancellationToken)
    {
        pending.Writer.TryComplete();
        if (pending.Reader.Count > 0)
            PendingOnStop(logger, pending.Reader.Count);
        return base.StopAsync(cancellationToken);
    }

    [LoggerMessage(Level = LogLevel.Critical, Message = "Audit buffer full or stopped. Event {EventId} could not be queued. Action {Action}, resource {ResourceId}")]
    private static partial void BufferFull(ILogger logger, Guid eventId, string action, string? resourceId);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Audit event {EventId} remains pending; retrying in 5 seconds")]
    private static partial void PublishFailed(ILogger logger, Guid eventId, Exception exception);
    [LoggerMessage(Level = LogLevel.Warning, Message = "Stopping with {Count} audit events in memory. Transactional outbox is required for restart durability")]
    private static partial void PendingOnStop(ILogger logger, int count);
}
