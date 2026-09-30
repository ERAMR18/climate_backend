using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Climate.Contracts.Audit;

public sealed class AuditOutboxMessage
{
    public Guid Id { get; set; }
    public string Payload { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public static class AuditOutboxModel
{
    public static void AddAuditOutbox(this ModelBuilder model)
    {
        var entity = model.Entity<AuditOutboxMessage>();
        entity.ToTable("AuditOutbox");
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Payload).IsRequired();
        entity.HasIndex(x => new { x.PublishedAt, x.CreatedAt });
    }
}

public sealed class OutboxAuditPublisher<TContext>(TContext db) : IAuditEventPublisher where TContext : DbContext
{
    public Task PublishAsync(AuditLogRequested auditEvent, CancellationToken cancellationToken)
    {
        db.Set<AuditOutboxMessage>().Add(new() { Id = auditEvent.EventId,
            CreatedAt = auditEvent.OccurredAt.UtcDateTime, Payload = JsonSerializer.Serialize(auditEvent) });
        // The action filter commits this row and the business data in one transaction.
        return Task.CompletedTask;
    }
}
