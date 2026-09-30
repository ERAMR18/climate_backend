namespace Climate.Alerts.Application.Abstractions;

public interface ISensorActivityClient
{
    Task<bool> IsActiveAsync(Guid sensorId, CancellationToken token);
}
