namespace Climate.Sensors.Domain.Communities;

public sealed class Community
{
    private Community()
    {
    }

    private Community(
        Guid id,
        string name,
        string? description,
        decimal latitude,
        decimal longitude,
        DateTimeOffset createdAt)
    {
        Id = id;
        Name = name.Trim();
        Description = NormalizeOptional(description);
        Latitude = latitude;
        Longitude = longitude;
        IsActive = true;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public decimal Latitude { get; private set; }
    public decimal Longitude { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public string? Municipality { get; private set; }
    public string? Department { get; private set; }
    public string? Country { get; private set; }
    public void SetGeography(string? municipality, string? department, string? country)
    { Municipality = NormalizeOptional(municipality); Department = NormalizeOptional(department); Country = NormalizeOptional(country); }
    public void SetStatus(bool active) => IsActive = active;

    public static Community Create(
        Guid id,
        string name,
        string? description,
        decimal latitude,
        decimal longitude,
        DateTimeOffset createdAt) =>
        new(id, name, description, latitude, longitude, createdAt);

    public void Update(string name, string? description, decimal latitude, decimal longitude, bool isActive)
    {
        Name = name.Trim();
        Description = NormalizeOptional(description);
        Latitude = latitude;
        Longitude = longitude;
        IsActive = isActive;
    }

    private static string? NormalizeOptional(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
