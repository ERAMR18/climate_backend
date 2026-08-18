namespace Climate.Identity.Application.Users;

public sealed record UpdateUserRequest(string Username, string Email, string Role);
