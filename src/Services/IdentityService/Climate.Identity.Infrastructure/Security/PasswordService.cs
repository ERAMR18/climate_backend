using Climate.Identity.Application.Abstractions;
using Climate.Identity.Domain.Users;
using Microsoft.AspNetCore.Identity;

namespace Climate.Identity.Infrastructure.Security;

internal sealed class PasswordService(IPasswordHasher<User> passwordHasher) : IPasswordService
{
    public string Hash(User user, string password) => passwordHasher.HashPassword(user, password);

    public PasswordCheckResult Verify(User user, string passwordHash, string password) =>
        passwordHasher.VerifyHashedPassword(user, passwordHash, password) switch
        {
            PasswordVerificationResult.Success => PasswordCheckResult.Success,
            PasswordVerificationResult.SuccessRehashNeeded => PasswordCheckResult.SuccessRehashNeeded,
            _ => PasswordCheckResult.Failed
        };
}
