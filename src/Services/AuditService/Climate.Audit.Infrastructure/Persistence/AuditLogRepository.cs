using Climate.Audit.Application.Abstractions;
using Climate.Audit.Domain.Auditing;
using Microsoft.EntityFrameworkCore;
namespace Climate.Audit.Infrastructure.Persistence;
internal sealed class AuditLogRepository(AuditDbContext db):IAuditLogRepository
{
    public Task<AuditLog?> GetByIdAsync(Guid id,CancellationToken ct)=>db.AuditLogs.AsNoTracking().FirstOrDefaultAsync(x=>x.Id==id,ct);
    public Task<bool> ExistsAsync(Guid id,CancellationToken ct)=>db.AuditLogs.AnyAsync(x=>x.Id==id,ct);
    public async Task<IReadOnlyCollection<AuditLog>> ListAsync(Guid? userId,string? action,string? resource,DateTimeOffset? from,DateTimeOffset? until,CancellationToken ct)
    {
        IQueryable<AuditLog> q=db.AuditLogs.AsNoTracking();
        if(userId.HasValue)q=q.Where(x=>x.UserId==userId); if(!string.IsNullOrWhiteSpace(action))q=q.Where(x=>x.Action==action);
        if(!string.IsNullOrWhiteSpace(resource))q=q.Where(x=>x.Resource==resource); if(from.HasValue)q=q.Where(x=>x.Timestamp>=from);
        if(until.HasValue)q=q.Where(x=>x.Timestamp<=until); return await q.OrderByDescending(x=>x.Timestamp).ToArrayAsync(ct);
    }
    public Task AddAsync(AuditLog log,CancellationToken ct)=>db.AuditLogs.AddAsync(log,ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
}
