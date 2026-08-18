namespace Climate.Sensors.Api.Configuration;

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";
    public const string HeaderName = "X-Internal-Api-Key";
    public string ApiKey { get; init; } = string.Empty;
}
