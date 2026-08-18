using Climate.Contracts.Sensors;

namespace Climate.Monitoring.Application.Abstractions;

public interface ISimulatedValueGenerator
{
    decimal NextValue(SensorType sensorType);
}
