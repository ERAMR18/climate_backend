using Climate.Sensors.Domain.Sensors;

namespace Climate.Sensors.Tests.Sensors;

public sealed class SensorDomainTests
{
    [Fact]
    public void CreateTrimsValuesNormalizesCodeAndStartsActive()
    {
        DateTimeOffset now=DateTimeOffset.UtcNow;
        Sensor sensor=Sensor.Create(Guid.NewGuid()," Station "," temp-01 "," Outside ",SensorType.Temperature," °C ",Guid.NewGuid(),14m,-90m,now);
        Assert.Equal("Station",sensor.Name); Assert.Equal("temp-01",sensor.Code); Assert.Equal("TEMP-01",sensor.NormalizedCode);
        Assert.Equal("Outside",sensor.Description); Assert.Equal("°C",sensor.Unit); Assert.True(sensor.IsActive); Assert.Equal(now,sensor.CreatedAt);
    }

    [Fact]
    public void UpdateReplacesMetadataAndTimestamp()
    {
        DateTimeOffset created=DateTimeOffset.UtcNow; Sensor sensor=Create(created); DateTimeOffset updated=created.AddMinutes(1);
        sensor.Update("Rain","rain-02",null,SensorType.Rainfall,"mm",Guid.NewGuid(),15m,-91m,updated);
        Assert.Equal("RAIN-02",sensor.NormalizedCode); Assert.Null(sensor.Description); Assert.Equal(SensorType.Rainfall,sensor.Type); Assert.Equal(updated,sensor.UpdatedAt);
    }

    [Fact]
    public void SetStatusPerformsLogicalDeactivationAndReactivation()
    {
        Sensor sensor=Create(DateTimeOffset.UtcNow); DateTimeOffset changed=DateTimeOffset.UtcNow.AddMinutes(1);
        sensor.SetStatus(false,changed); Assert.False(sensor.IsActive); Assert.Equal(changed,sensor.UpdatedAt);
        sensor.SetStatus(true,changed.AddMinutes(1)); Assert.True(sensor.IsActive);
    }

    private static Sensor Create(DateTimeOffset now)=>Sensor.Create(Guid.NewGuid(),"Sensor","S-1",null,SensorType.Humidity,"%",Guid.NewGuid(),0m,0m,now);
}
