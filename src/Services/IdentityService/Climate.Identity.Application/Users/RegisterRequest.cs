namespace Climate.Identity.Application.Users;

public sealed record RegisterRequest(string Username, string Email, string Password);
