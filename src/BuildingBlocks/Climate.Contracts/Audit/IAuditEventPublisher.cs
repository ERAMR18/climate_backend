namespace Climate.Contracts.Audit;

public interface IAuditEventPublisher
{
    Task PublishAsync(AuditLogRequested auditEvent, CancellationToken cancellationToken);
}
