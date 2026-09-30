namespace Climate.Monitoring.Domain.Readings;

public sealed class SensorReading
{
    private SensorReading()
    {
    }

    private SensorReading(
        Guid id,
        Guid sensorId,
        Guid communityId,
        SensorType sensorType,
        decimal value,
        string unit,
        DateTimeOffset recordedAt)
    {
        SensorWasActive = true;
        Id = id;
        SensorId = sensorId;
        CommunityId = communityId;
        SensorType = sensorType;
        Value = value;
        Unit = unit.Trim();
        RecordedAt = recordedAt.ToUniversalTime();
    }

    public bool? SensorWasActive { get; private set; }
    public Guid Id { get; private set; }
    public Guid SensorId { get; private set; }
    public Guid CommunityId { get; private set; }
    public SensorType SensorType { get; private set; }
    public decimal Value { get; private set; }
    public string Unit { get; private set; } = string.Empty;
    public DateTimeOffset RecordedAt { get; private set; }

    public static SensorReading Create(
        Guid id,
        Guid sensorId,
        Guid communityId,
        SensorType sensorType,
        decimal value,
        string unit,
        DateTimeOffset recordedAt) =>
        new(id, sensorId, communityId, sensorType, value, unit, recordedAt);
}
