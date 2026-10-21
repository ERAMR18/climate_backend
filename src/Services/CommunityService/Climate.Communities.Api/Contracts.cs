using Climate.Communities.Api.Persistence;

namespace Climate.Communities.Api;

public sealed record CommunityRequest(string Name, string? Description, decimal Latitude, decimal Longitude,
    bool IsActive = true, string? Municipality = null, string? Department = null, string? Country = null)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Name) && Name.Trim().Length <= 120
        && Description?.Length is not > 500 && Latitude is >= -90 and <= 90 && Longitude is >= -180 and <= 180
        && Municipality?.Length is not > 120 && Department?.Length is not > 120 && Country?.Length is not > 120;

    public void Apply(Community entity)
    {
        entity.Name = Name.Trim(); entity.Description = Normalize(Description);
        entity.Latitude = Latitude; entity.Longitude = Longitude; entity.IsActive = IsActive;
        entity.Municipality = Normalize(Municipality); entity.Department = Normalize(Department); entity.Country = Normalize(Country);
    }
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

public sealed record CommunityResponse(Guid Id, string Name, string? Description, decimal Latitude, decimal Longitude,
    bool IsActive, DateTimeOffset CreatedAt, string? Municipality, string? Department, string? Country, int SensorCount)
{
    public static CommunityResponse From(Community entity, int count = 0) => new(entity.Id, entity.Name, entity.Description,
        entity.Latitude, entity.Longitude, entity.IsActive, entity.CreatedAt, entity.Municipality, entity.Department, entity.Country, count);
}
