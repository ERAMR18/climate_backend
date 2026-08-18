using Climate.Sensors.Domain.Sensors;

namespace Climate.Sensors.Application.Sensors;

public sealed record CreateSensorRequest(
    string Name,
    string Code,
    string? Description,
    SensorType Type,
    string Unit,
    Guid CommunityId,
    decimal Latitude,
    decimal Longitude);

public sealed record UpdateSensorRequest(
    string Name,
    string Code,
    string? Description,
    SensorType Type,
    string Unit,
    Guid CommunityId,
    decimal Latitude,
    decimal Longitude);

public sealed record SensorResponse(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    SensorType Type,
    string Unit,
    Guid CommunityId,
    string CommunityName,
    decimal Latitude,
    decimal Longitude,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public static SensorResponse FromEntity(Sensor sensor, string? communityName = null) =>
        new(
            sensor.Id,
            sensor.Name,
            sensor.Code,
            sensor.Description,
            sensor.Type,
            sensor.Unit,
            sensor.CommunityId,
            communityName ?? sensor.Community.Name,
            sensor.Latitude,
            sensor.Longitude,
            sensor.IsActive,
            sensor.CreatedAt,
            sensor.UpdatedAt);
}
