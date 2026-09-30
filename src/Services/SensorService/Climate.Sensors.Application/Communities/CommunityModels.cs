using Climate.Sensors.Domain.Communities;

namespace Climate.Sensors.Application.Communities;

public sealed record CreateCommunityRequest(
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude, string? Municipality = null, string? Department = null, string? Country = null);

public sealed record UpdateCommunityRequest(
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude,
    bool IsActive, string? Municipality = null, string? Department = null, string? Country = null);

public sealed record CommunityResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude,
    bool IsActive,
    DateTimeOffset CreatedAt, string? Municipality = null, string? Department = null, string? Country = null, int SensorCount = 0)
{
    public static CommunityResponse FromEntity(Community community) =>
        new(
            community.Id,
            community.Name,
            community.Description,
            community.Latitude,
            community.Longitude,
            community.IsActive,
            community.CreatedAt, community.Municipality, community.Department, community.Country);
}
