namespace Climate.Monitoring.Infrastructure.Clients;

public sealed class AlertServiceOptions
{
    public const string SectionName = "AlertService";
    public Uri BaseUrl { get; init; } = null!;
    public string ApiKey { get; init; } = string.Empty;
}
