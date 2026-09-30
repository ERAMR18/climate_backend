using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Climate.Contracts.Audit;

public sealed partial class OutboxAuditWorker<TContext>(IServiceScopeFactory scopes, IAuditEventTransport transport,
    ILogger<OutboxAuditWorker<TContext>> logger) : BackgroundService where TContext : DbContext
{
    public async Task DispatchOnceAsync(CancellationToken token)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<TContext>();
        var messages = await db.Set<AuditOutboxMessage>().Where(x => x.PublishedAt == null)
            .OrderBy(x => x.CreatedAt).Take(100).ToArrayAsync(token);
        foreach (var row in messages)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(15));
            var message = JsonSerializer.Deserialize<AuditLogRequested>(row.Payload)
                ?? throw new InvalidOperationException($"Invalid outbox row {row.Id}");
            await transport.SendAsync(message, timeout.Token);
            row.PublishedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(token);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await DispatchOnceAsync(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception exception) { DispatchFailed(logger, exception); }
            try { await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Outbox dispatch failed. Unconfirmed events remain in the database for retry.")]
    private static partial void DispatchFailed(ILogger logger, Exception exception);
}
