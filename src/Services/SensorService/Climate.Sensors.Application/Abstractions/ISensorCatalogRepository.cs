using Climate.Sensors.Domain.Communities;
using Climate.Sensors.Domain.Sensors;

namespace Climate.Sensors.Application.Abstractions;

public interface ISensorCatalogRepository
{
    Task<IReadOnlyCollection<Climate.Sensors.Application.Communities.CommunityResponse>> SearchCommunitiesAsync(string? search, bool? isActive, string? municipality, string? department, CancellationToken token);
    Task<IReadOnlyCollection<Sensor>> SearchSensorsAsync(Guid? communityId, SensorType? type, bool? isActive, string? code, string? search, CancellationToken token);
    Task<IReadOnlyCollection<Community>> ListCommunitiesAsync(CancellationToken cancellationToken);
    Task<Community?> GetCommunityAsync(Guid id, CancellationToken cancellationToken);
    Task<int> CountSensorsAsync(Guid communityId, CancellationToken token);
    Task<bool> CommunityNameExistsAsync(string name, Guid? excludingId, CancellationToken cancellationToken);
    Task AddCommunityAsync(Community community, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<Sensor>> ListSensorsAsync(CancellationToken cancellationToken);
    Task<Sensor?> GetSensorAsync(Guid id, CancellationToken cancellationToken);
    Task<bool> SensorCodeExistsAsync(string code, Guid? excludingId, CancellationToken cancellationToken);
    Task AddSensorAsync(Sensor sensor, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
