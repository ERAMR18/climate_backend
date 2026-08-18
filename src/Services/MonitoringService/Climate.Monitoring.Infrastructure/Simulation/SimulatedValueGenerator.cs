using Climate.Contracts.Sensors;
using Climate.Monitoring.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace Climate.Monitoring.Infrastructure.Simulation;

internal sealed class SimulatedValueGenerator(IOptions<SimulationOptions> options) : ISimulatedValueGenerator
{
    private readonly SimulationOptions _options = options.Value;

    public decimal NextValue(SensorType sensorType)
    {
        SensorRangeOptions range = sensorType switch
        {
            SensorType.Temperature => _options.Temperature,
            SensorType.Humidity => _options.Humidity,
            SensorType.WindSpeed => _options.WindSpeed,
            SensorType.Rainfall => _options.Rainfall,
            SensorType.WaterLevel => _options.WaterLevel,
            _ => throw new ArgumentOutOfRangeException(nameof(sensorType), sensorType, "Unsupported sensor type.")
        };

        decimal value = range.Minimum + (decimal)Random.Shared.NextDouble() * (range.Maximum - range.Minimum);
        return decimal.Round(value, range.DecimalPlaces, MidpointRounding.AwayFromZero);
    }
}
