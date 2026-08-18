using Climate.Sensors.Domain.Communities;

namespace Climate.Sensors.Application.Communities;

public sealed record CreateCommunityRequest(
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude);

public sealed record UpdateCommunityRequest(
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude,
    bool IsActive);

public sealed record CommunityResponse(
    Guid Id,
    string Name,
    string? Description,
    decimal Latitude,
    decimal Longitude,
    bool IsActive,
    DateTimeOffset CreatedAt)
{
    public static CommunityResponse FromEntity(Community community) =>
        new(
            community.Id,
            community.Name,
            community.Description,
            community.Latitude,
            community.Longitude,
            community.IsActive,
            community.CreatedAt);
}
