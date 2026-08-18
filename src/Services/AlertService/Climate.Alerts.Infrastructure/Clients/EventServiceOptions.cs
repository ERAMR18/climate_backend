namespace Climate.Alerts.Infrastructure.Clients;

public sealed class EventServiceOptions
{
    public const string SectionName = "EventService";
    public string BaseUrl { get; init; } = string.Empty;
    public string ApiKey { get; init; } = string.Empty;
}
