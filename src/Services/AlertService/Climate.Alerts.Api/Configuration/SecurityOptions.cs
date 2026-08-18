namespace Climate.Alerts.Api.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Issuer { get; init; } = string.Empty;
    public string Audience { get; init; } = string.Empty;
    public string SigningKey { get; init; } = string.Empty;
}

public sealed class InternalApiOptions
{
    public const string SectionName = "InternalApi";
    public const string HeaderName = "X-Internal-Api-Key";
    public string ApiKey { get; init; } = string.Empty;
}
