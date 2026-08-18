using Climate.SharedKernel.Results;

namespace Climate.Identity.Application.Users;

public interface IUserService
{
    Task<Result<UserResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);

    Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);

    Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken);

    Task<Result<UserResponse>> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken cancellationToken);

    Task<Result> SetStatusAsync(Guid id, UpdateUserStatusRequest request, CancellationToken cancellationToken);
}
