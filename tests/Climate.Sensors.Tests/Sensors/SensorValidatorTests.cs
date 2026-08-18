using Climate.Sensors.Application.Sensors;
using Climate.Sensors.Domain.Sensors;

namespace Climate.Sensors.Tests.Sensors;

public sealed class SensorValidatorTests
{
    [Theory]
    [InlineData(91, 0)]
    [InlineData(-91, 0)]
    [InlineData(0, 181)]
    [InlineData(0, -181)]
    public async Task ValidatorRejectsCoordinatesOutsideEarth(decimal latitude, decimal longitude)
    {
        var validator = new CreateSensorRequestValidator();
        var request = new CreateSensorRequest(
            "Sensor",
            "CODE-1",
            null,
            SensorType.Temperature,
            "°C",
            Guid.NewGuid(),
            latitude,
            longitude);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task ValidatorRejectsUnknownSensorType()
    {
        var validator = new CreateSensorRequestValidator();
        var request = new CreateSensorRequest(
            "Sensor",
            "CODE-1",
            null,
            (SensorType)999,
            "unit",
            Guid.NewGuid(),
            0,
            0);

        var result = await validator.ValidateAsync(request, CancellationToken.None);

        Assert.False(result.IsValid);
    }
}
