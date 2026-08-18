using Climate.Alerts.Domain.Alerts;

namespace Climate.Alerts.Application.Abstractions;

public interface IEventHistoryClient
{
    Task RecordAsync(ClimateAlert alert, CancellationToken cancellationToken);
}
