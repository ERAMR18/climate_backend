namespace Climate.Monitoring.Infrastructure.Simulation;

public sealed class SimulationOptions
{
    public const string SectionName = "Simulation";
    public bool Enabled { get; init; } = true;
    public int IntervalSeconds { get; init; } = 5;
    public SensorRangeOptions Temperature { get; init; } = new();
    public SensorRangeOptions Humidity { get; init; } = new();
    public SensorRangeOptions WindSpeed { get; init; } = new();
    public SensorRangeOptions Rainfall { get; init; } = new();
    public SensorRangeOptions WaterLevel { get; init; } = new();
}

public sealed class SensorRangeOptions
{
    public decimal Minimum { get; init; }
    public decimal Maximum { get; init; }
    public int DecimalPlaces { get; init; } = 1;
}
