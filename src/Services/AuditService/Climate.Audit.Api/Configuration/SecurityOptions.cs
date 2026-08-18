namespace Climate.Audit.Api.Configuration;
public sealed class JwtOptions { public const string SectionName="Jwt"; public string Issuer{get;init;}=""; public string Audience{get;init;}=""; public string SigningKey{get;init;}=""; }
public sealed class InternalApiOptions { public const string SectionName="InternalApi"; public string ApiKey{get;init;}=""; }
