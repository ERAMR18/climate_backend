using Climate.SharedKernel.Results;
namespace Climate.Audit.Application.Auditing;
public interface IAuditService
{
    Task<IReadOnlyCollection<AuditResponse>> ListAsync(AuditFilter filter, CancellationToken cancellationToken);
    Task<Result<AuditResponse>> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<AuditResponse>> RecordAsync(RecordAuditRequest request, CancellationToken cancellationToken);
}
