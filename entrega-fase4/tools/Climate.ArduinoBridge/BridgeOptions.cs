namespace Climate.ArduinoBridge;

public sealed class BridgeOptions
{
    public ArduinoOptions Arduino { get; set; } = new();
    public BackendOptions Backend { get; set; } = new();
    public DeviceOptions Device { get; set; } = new();
    public BufferOptions Buffer { get; set; } = new();
    public bool IsValid() => Arduino.BaudRate > 0 && Arduino.ReconnectSeconds is >= 1 and <= 300
        && Uri.TryCreate(Backend.BaseUrl, UriKind.Absolute, out Uri? uri)
        && (uri.Scheme == "https" || (Backend.AllowLocalHttp && uri.IsLoopback && uri.Scheme == "http"))
        && Backend.TelemetryPath.StartsWith("/api/", StringComparison.Ordinal)
        && !Backend.TelemetryPath.StartsWith("//", StringComparison.Ordinal)
        && Backend.TimeoutSeconds is >= 1 and <= 120
        && Guid.TryParse(Device.SensorId, out Guid sensorId) && sensorId != Guid.Empty
        && !string.IsNullOrWhiteSpace(Device.DeviceId) && Device.DeviceId.Length <= 100
        && !string.IsNullOrWhiteSpace(Backend.DeviceToken)
        && Buffer.MaxReadings is >= 1 and <= 1000000 && Buffer.RetentionHours is >= 1 and <= 8760;
}
public sealed class ArduinoOptions
{
    public string PortName { get; set; } = "";
    public int BaudRate { get; set; } = 9600;
    public int ReconnectSeconds { get; set; } = 5;
    public bool Simulate { get; set; }
}
public sealed class BackendOptions
{
    public string BaseUrl { get; set; } = "https://localhost:8080";
    public string TelemetryPath { get; set; } = "/api/monitoring/telemetry";
    public int TimeoutSeconds { get; set; } = 10;
    public bool AllowLocalHttp { get; set; }
    public string DeviceToken { get; set; } = "";
}
public sealed class DeviceOptions
{
    public string DeviceId { get; set; } = "arduino-temp-001";
    public string SensorId { get; set; } = "";
}
public sealed class BufferOptions
{
    public string Path { get; set; } = "arduino-buffer.db";
    public int MaxReadings { get; set; } = 10000;
    public int RetentionHours { get; set; } = 168;
}
