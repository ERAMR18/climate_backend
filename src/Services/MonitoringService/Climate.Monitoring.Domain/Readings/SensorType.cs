namespace Climate.Monitoring.Domain.Readings;

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
