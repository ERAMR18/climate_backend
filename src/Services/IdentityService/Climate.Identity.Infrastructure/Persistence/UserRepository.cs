using Climate.Identity.Application.Abstractions;
using Climate.Identity.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace Climate.Identity.Infrastructure.Persistence;

internal sealed class UserRepository(IdentityDbContext dbContext) : IUserRepository
{
    public async Task AddAsync(User user, CancellationToken cancellationToken) =>
        await dbContext.Users.AddAsync(user, cancellationToken);

    public Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Users.SingleOrDefaultAsync(user => user.Id == id, cancellationToken);

    public Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken)
    {
        string normalizedLogin = login.Trim().ToUpperInvariant();
        return dbContext.Users.SingleOrDefaultAsync(
            user => user.NormalizedEmail == normalizedLogin || user.NormalizedUsername == normalizedLogin,
            cancellationToken);
    }

    public Task<bool> UsernameExistsAsync(
        string username,
        Guid? excludingId,
        CancellationToken cancellationToken)
    {
        string normalizedUsername = username.Trim().ToUpperInvariant();
        return dbContext.Users.AnyAsync(
            user => user.NormalizedUsername == normalizedUsername &&
                    (!excludingId.HasValue || user.Id != excludingId.Value),
            cancellationToken);
    }

    public Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken)
    {
        string normalizedEmail = email.Trim().ToUpperInvariant();
        return dbContext.Users.AnyAsync(
            user => user.NormalizedEmail == normalizedEmail &&
                    (!excludingId.HasValue || user.Id != excludingId.Value),
            cancellationToken);
    }

    public async Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken) =>
        await dbContext.Users.AsNoTracking().OrderBy(user => user.Username).ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyCollection<User>> SearchAsync(string? search, string? role, bool? isActive, CancellationToken token)
    {
        var query = dbContext.Users.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search)) { var term = search.Trim().ToUpperInvariant(); query = query.Where(x => x.NormalizedUsername.Contains(term) || x.NormalizedEmail.Contains(term)); }
        if (!string.IsNullOrWhiteSpace(role)) query = query.Where(x => x.Role == role);
        if (isActive.HasValue) query = query.Where(x => x.IsActive == isActive.Value);
        return await query.OrderBy(x => x.Username).ToArrayAsync(token);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        dbContext.SaveChangesAsync(cancellationToken);
}
