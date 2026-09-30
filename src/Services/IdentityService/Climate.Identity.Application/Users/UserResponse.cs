using Climate.Identity.Domain.Users;

namespace Climate.Identity.Application.Users;

public sealed record UserResponse(
    Guid Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt, DateTimeOffset? LastLoginAt = null)
{
    public static UserResponse FromUser(User user) =>
        new(
            user.Id,
            user.Username,
            user.Email,
            user.Role,
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt, user.LastLoginAt);
}
