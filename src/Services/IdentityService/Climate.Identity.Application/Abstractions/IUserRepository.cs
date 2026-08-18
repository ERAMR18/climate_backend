using Climate.Identity.Domain.Users;

namespace Climate.Identity.Application.Abstractions;

public interface IUserRepository
{
    Task AddAsync(User user, CancellationToken cancellationToken);

    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByLoginAsync(string login, CancellationToken cancellationToken);

    Task<bool> UsernameExistsAsync(string username, Guid? excludingId, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, Guid? excludingId, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<User>> ListAsync(CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
