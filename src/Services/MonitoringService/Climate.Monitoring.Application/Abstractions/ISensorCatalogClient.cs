using Climate.Contracts.Sensors;

namespace Climate.Monitoring.Application.Abstractions;

public interface ISensorCatalogClient
{
    Task<IReadOnlyCollection<SensorSummary>> GetActiveSensorsAsync(CancellationToken cancellationToken);
}
