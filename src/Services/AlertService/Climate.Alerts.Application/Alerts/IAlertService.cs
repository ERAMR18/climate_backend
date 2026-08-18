using Climate.Contracts.Monitoring;
using Climate.SharedKernel.Results;

namespace Climate.Alerts.Application.Alerts;

public interface IAlertService
{
    Task<IReadOnlyCollection<AlertResponse>> ListAsync(AlertFilter filter, CancellationToken cancellationToken);
    Task<Result<AlertResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<IReadOnlyCollection<AlertResponse>>> EvaluateAsync(
        SensorReadingRecorded reading,
        CancellationToken cancellationToken);
    Task<Result> ResolveAsync(Guid id, CancellationToken cancellationToken);
}
