using Climate.Sensors.Domain.Communities;

namespace Climate.Sensors.Domain.Sensors;

public sealed class Sensor
{
    private Sensor()
    {
    }

    private Sensor(
        Guid id,
        string name,
        string code,
        string? description,
        SensorType type,
        string unit,
        Guid communityId,
        decimal latitude,
        decimal longitude,
        DateTimeOffset now)
    {
        Id = id;
        Apply(name, code, description, type, unit, communityId, latitude, longitude);
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Code { get; private set; } = string.Empty;
    public string NormalizedCode { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public SensorType Type { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public Guid CommunityId { get; private set; }
    public Community Community { get; private set; } = null!;
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    public static Sensor Create(
        Guid id,
        string name,
        string code,
        string? description,
        SensorType type,
        string unit,
        Guid communityId,
        decimal latitude,
        decimal longitude,
        DateTimeOffset now) =>
        new(id, name, code, description, type, unit, communityId, latitude, longitude, now);

    public void Update(
        string name,
        string code,
        string? description,
        SensorType type,
        string unit,
        Guid communityId,
        decimal latitude,
        decimal longitude,
        DateTimeOffset now)
    {
        Apply(name, code, description, type, unit, communityId, latitude, longitude);
        UpdatedAt = now;
    }

    public void SetStatus(bool isActive, DateTimeOffset now)
    {
        IsActive = isActive;
        UpdatedAt = now;
    }

    private void Apply(
        string name,
        string code,
        string? description,
        SensorType type,
        string unit,
        Guid communityId,
        decimal latitude,
        decimal longitude)
    {
        Name = name.Trim();
        Code = code.Trim();
        NormalizedCode = code.Trim().ToUpperInvariant();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Type = type;
        Unit = unit.Trim();
        CommunityId = communityId;
        Latitude = latitude;
        Longitude = longitude;
    }
}
