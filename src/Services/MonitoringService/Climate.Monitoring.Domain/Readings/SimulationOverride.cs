namespace Climate.Monitoring.Domain.Readings;
public sealed class SimulationOverride
{
    public Guid SensorId { get; set; }
    public decimal Value { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
