namespace Climate.Monitoring.Infrastructure.Clients;

public sealed class SensorServiceOptions
{
    public const string SectionName = "SensorService";
    public Uri BaseUrl { get; init; } = null!;
    public string ApiKey { get; init; } = string.Empty;
}
