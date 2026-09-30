namespace Climate.Sensors.Domain.Sensors;

public enum SensorType
{
    Temperature,
    Humidity,
    WindSpeed,
    Rainfall,
    WaterLevel, // Legacy value 4 remains unchanged.
    RiverLevel,
    ReservoirLevel,
    Smoke,
    Other
}
