using Climate.Identity.Domain.Users;

namespace Climate.Identity.Application.Abstractions;

public interface IPasswordService
{
    string Hash(User user, string password);

    PasswordCheckResult Verify(User user, string passwordHash, string password);
}

public enum PasswordCheckResult
{
    Failed,
    Success,
    SuccessRehashNeeded
}
