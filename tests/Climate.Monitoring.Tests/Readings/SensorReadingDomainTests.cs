using Climate.Monitoring.Domain.Readings;

namespace Climate.Monitoring.Tests.Readings;

public sealed class SensorReadingDomainTests
{
    [Fact]
    public void CreatePreservesMeasurementAndTrimsUnit()
    {
        Guid id=Guid.NewGuid(); Guid sensorId=Guid.NewGuid(); DateTimeOffset timestamp=DateTimeOffset.UtcNow;
        SensorReading reading=SensorReading.Create(id,sensorId,Guid.NewGuid(),SensorType.WindSpeed,27.4m," km/h ",timestamp);
        Assert.Equal(id,reading.Id); Assert.Equal(sensorId,reading.SensorId); Assert.Equal(27.4m,reading.Value); Assert.Equal("km/h",reading.Unit); Assert.Equal(timestamp,reading.RecordedAt);
    }
}
