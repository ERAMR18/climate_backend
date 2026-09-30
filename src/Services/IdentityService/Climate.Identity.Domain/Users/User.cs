namespace Climate.Identity.Domain.Users;

public sealed class User
{
    private User()
    {
    }

    private User(Guid id, string username, string email, string passwordHash, string role, DateTimeOffset now)
    {
        Id = id;
        Username = username;
        NormalizedUsername = Normalize(username);
        Email = email;
        NormalizedEmail = Normalize(email);
        PasswordHash = passwordHash;
        Role = role;
        IsActive = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public string Username { get; private set; } = string.Empty;

    public string NormalizedUsername { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string NormalizedEmail { get; private set; } = string.Empty;

    public string PasswordHash { get; private set; } = string.Empty;

    public string Role { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? LastLoginAt { get; private set; }
    public void RecordLogin(DateTimeOffset now) => LastLoginAt = now;

    public static User Create(
        Guid id,
        string username,
        string email,
        string passwordHash,
        string role,
        DateTimeOffset now) =>
        new(id, username.Trim(), email.Trim(), passwordHash, role, now);

    public void Update(string username, string email, string role, DateTimeOffset now)
    {
        Username = username.Trim();
        NormalizedUsername = Normalize(username);
        Email = email.Trim();
        NormalizedEmail = Normalize(email);
        Role = role;
        UpdatedAt = now;
    }

    public void SetStatus(bool isActive, DateTimeOffset now)
    {
        IsActive = isActive;
        UpdatedAt = now;
    }

    public void SetPasswordHash(string passwordHash, DateTimeOffset now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }

    private static string Normalize(string value) => value.Trim().ToUpperInvariant();
}
