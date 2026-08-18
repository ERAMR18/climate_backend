using Climate.Monitoring.Domain.Readings;

namespace Climate.Monitoring.Application.Readings;

public sealed record CreateReadingRequest(Guid SensorId, decimal Value, DateTimeOffset? RecordedAt = null);

public sealed record HistoryFilter(Guid? CommunityId, DateTimeOffset? From, DateTimeOffset? To);

public sealed record SensorReadingResponse(
    Guid Id,
    Guid SensorId,
    Guid CommunityId,
    SensorType SensorType,
    decimal Value,
    string Unit,
    DateTimeOffset RecordedAt)
{
    public static SensorReadingResponse FromEntity(SensorReading reading) =>
        new(
            reading.Id,
            reading.SensorId,
            reading.CommunityId,
            reading.SensorType,
            reading.Value,
            reading.Unit,
            reading.RecordedAt);
}

public sealed record ChartPoint(DateTimeOffset Timestamp, decimal Value);
public sealed record SensorChartResponse(Guid SensorId, string Unit, IReadOnlyCollection<ChartPoint> Data);
public sealed record SimulationStatusResponse(bool IsRunning);
