namespace Climate.Identity.Application.Users;

public sealed record CreateUserRequest(string Username, string Email, string Password, string Role);
