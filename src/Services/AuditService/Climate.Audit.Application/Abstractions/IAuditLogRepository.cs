using Climate.Audit.Domain.Auditing;

namespace Climate.Audit.Application.Abstractions;

public interface IAuditLogRepository
{
    Task<AuditLog?> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<AuditLog>> ListAsync(Guid? userId, string? action, string? resource,
        DateTimeOffset? from, DateTimeOffset? until, CancellationToken cancellationToken);
    Task AddAsync(AuditLog log, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}
