using System.Text.Json;

namespace Climate.ArduinoBridge;

public sealed record SerialReading([property: System.Text.Json.Serialization.JsonRequired] int ProtocolVersion,
    [property: System.Text.Json.Serialization.JsonRequired] string DeviceId,
    [property: System.Text.Json.Serialization.JsonRequired] string SensorType,
    [property: System.Text.Json.Serialization.JsonRequired] decimal Value,
    [property: System.Text.Json.Serialization.JsonRequired] string Unit,
    [property: System.Text.Json.Serialization.JsonRequired] long Sequence, DateTimeOffset? Timestamp, string Source = "ARDUINO");
public sealed record TelemetryReading(Guid ReadingId, Guid SensorId, string DeviceId,
    decimal Value, string Unit, DateTimeOffset MeasuredAt, string Source);

public static class Protocol
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static TelemetryReading Parse(string line, DeviceOptions device, DateTimeOffset receivedAt)
    {
        if (line.Length > 4096) throw new FormatException("Serial frame exceeds 4096 characters.");
        SerialReading reading = JsonSerializer.Deserialize<SerialReading>(line, Json)
            ?? throw new FormatException("Empty serial frame.");
        if (reading.ProtocolVersion != 1 || reading.DeviceId != device.DeviceId
            || reading.SensorType != "TEMPERATURE" || reading.Unit != "C"
            || reading.Value is < -55 or > 150 || reading.Sequence < 0
            || reading.Source is not ("ARDUINO" or "SIMULATED"))
            throw new FormatException("Invalid protocol, device, temperature, source or unit.");
        DateTimeOffset measuredAt = reading.Timestamp ?? receivedAt;
        if (measuredAt > receivedAt.AddMinutes(5) || measuredAt < receivedAt.AddDays(-7))
            throw new FormatException("Timestamp outside accepted window.");
        // UUID is generated once, before durable enqueue; HTTP retries reuse it.
        return new(Guid.NewGuid(), Guid.Parse(device.SensorId), reading.DeviceId,
            decimal.Round(reading.Value, 4), reading.Unit, measuredAt.ToUniversalTime(), reading.Source);
    }
}
