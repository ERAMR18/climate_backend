namespace Climate.Identity.Application.Users;

public sealed record AccessToken(string Value, DateTimeOffset ExpiresAt);
