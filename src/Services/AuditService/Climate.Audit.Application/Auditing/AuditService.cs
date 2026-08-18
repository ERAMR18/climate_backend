using Climate.Audit.Application.Abstractions;
using Climate.Audit.Domain.Auditing;
using Climate.SharedKernel.Results;

namespace Climate.Audit.Application.Auditing;

public sealed class AuditService(IAuditLogRepository repository) : IAuditService
{
    public async Task<IReadOnlyCollection<AuditResponse>> ListAsync(AuditFilter filter, CancellationToken cancellationToken) =>
        (await repository.ListAsync(filter.UserId,filter.Action,filter.Resource,filter.From,filter.To,cancellationToken)).Select(AuditResponse.FromEntity).ToArray();
    public async Task<Result<AuditResponse>> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        AuditLog? log=await repository.GetByIdAsync(id,cancellationToken);
        return log is null ? Result.Failure<AuditResponse>(AuditErrors.NotFound) : Result.Success(AuditResponse.FromEntity(log));
    }
    public async Task<Result<AuditResponse>> RecordAsync(RecordAuditRequest request, CancellationToken cancellationToken)
    {
        if (request.EventId==Guid.Empty || request.UserId==Guid.Empty || string.IsNullOrWhiteSpace(request.UserName) ||
            string.IsNullOrWhiteSpace(request.Action) || string.IsNullOrWhiteSpace(request.Resource) || string.IsNullOrWhiteSpace(request.Description))
            return Result.Failure<AuditResponse>(AuditErrors.Invalid);
        AuditLog? existing=await repository.GetByIdAsync(request.EventId,cancellationToken);
        if(existing is not null) return Result.Success(AuditResponse.FromEntity(existing));
        AuditLog log=AuditLog.Create(request.EventId,request.UserId,request.UserName,request.Action,request.Resource,request.ResourceId,request.Description,request.IpAddress,request.Timestamp);
        await repository.AddAsync(log,cancellationToken); await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(AuditResponse.FromEntity(log));
    }
}
public static class AuditErrors
{
    public static readonly ApplicationError NotFound=ApplicationError.NotFound("audit.not_found","The requested audit log does not exist.");
    public static readonly ApplicationError Invalid=ApplicationError.Validation("audit.invalid","Required audit fields are missing.");
}
