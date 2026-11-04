namespace Climate.Monitoring.Application.Readings;

public sealed class TelemetryOptions
{
    public decimal MinimumTemperature { get; set; } = -55;
    public decimal MaximumTemperature { get; set; } = 150;
    public List<TelemetryDevice> Devices { get; set; } = [];
}

public sealed class TelemetryDevice
{
    public string DeviceId { get; set; } = "";
    public Guid SensorId { get; set; }
    public string TokenSha256 { get; set; } = "";
    public bool IsActive { get; set; } = true;
    public string Source { get; set; } = "ARDUINO";
}

public sealed record TelemetryRequest(Guid ReadingId, Guid SensorId, string DeviceId,
    [property: System.Text.Json.Serialization.JsonRequired] decimal Value, string Unit, DateTimeOffset MeasuredAt, string Source);
