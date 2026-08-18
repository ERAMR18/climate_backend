namespace Climate.Identity.Infrastructure.Seeding;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public string Username { get; init; } = string.Empty;

    public string Email { get; init; } = string.Empty;

    public string Password { get; init; } = string.Empty;
}
