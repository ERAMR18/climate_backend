namespace Climate.Identity.Application.Users;

public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserResponse User);
