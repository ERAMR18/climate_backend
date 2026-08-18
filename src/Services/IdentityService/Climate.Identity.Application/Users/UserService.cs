using Climate.Contracts.Identity;
using Climate.Identity.Application.Abstractions;
using Climate.Identity.Domain.Users;
using Climate.SharedKernel.Results;
using FluentValidation;

namespace Climate.Identity.Application.Users;

public sealed class UserService(
    IUserRepository repository,
    IPasswordService passwordService,
    IJwtTokenGenerator tokenGenerator,
    IValidator<RegisterRequest> registerValidator,
    IValidator<LoginRequest> loginValidator,
    IValidator<UpdateUserRequest> updateValidator,
    TimeProvider timeProvider) : IUserService
{
    public async Task<Result<UserResponse>> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await registerValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<UserResponse>(ValidationError.From(validation));
        }

        if (await repository.UsernameExistsAsync(request.Username, null, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.UsernameConflict);
        }

        if (await repository.EmailExistsAsync(request.Email, null, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.EmailConflict);
        }

        DateTimeOffset now = timeProvider.GetUtcNow();
        User user = User.Create(
            Guid.NewGuid(),
            request.Username,
            request.Email,
            passwordHash: string.Empty,
            SystemRoles.Viewer,
            now);

        user.SetPasswordHash(passwordService.Hash(user, request.Password), now);
        await repository.AddAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        return Result.Success(UserResponse.FromUser(user));
    }

    public async Task<Result<LoginResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var validation = await loginValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<LoginResponse>(ValidationError.From(validation));
        }

        User? user = await repository.GetByLoginAsync(request.Login, cancellationToken);
        if (user is null)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidCredentials);
        }

        PasswordCheckResult passwordResult = passwordService.Verify(user, user.PasswordHash, request.Password);
        if (passwordResult == PasswordCheckResult.Failed)
        {
            return Result.Failure<LoginResponse>(UserErrors.InvalidCredentials);
        }

        if (!user.IsActive)
        {
            return Result.Failure<LoginResponse>(UserErrors.Inactive);
        }

        if (passwordResult == PasswordCheckResult.SuccessRehashNeeded)
        {
            user.SetPasswordHash(passwordService.Hash(user, request.Password), timeProvider.GetUtcNow());
            await repository.SaveChangesAsync(cancellationToken);
        }

        AccessToken token = tokenGenerator.Generate(user);
        return Result.Success(new LoginResponse(token.Value, token.ExpiresAt, UserResponse.FromUser(user)));
    }

    public async Task<Result<UserResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        User? user = await repository.GetByIdAsync(id, cancellationToken);
        return user is null
            ? Result.Failure<UserResponse>(UserErrors.NotFound)
            : Result.Success(UserResponse.FromUser(user));
    }

    public async Task<IReadOnlyCollection<UserResponse>> GetAllAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<User> users = await repository.ListAsync(cancellationToken);
        return users.Select(UserResponse.FromUser).ToArray();
    }

    public async Task<Result<UserResponse>> UpdateAsync(
        Guid id,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await updateValidator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<UserResponse>(ValidationError.From(validation));
        }

        User? user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure<UserResponse>(UserErrors.NotFound);
        }

        if (await repository.UsernameExistsAsync(request.Username, id, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.UsernameConflict);
        }

        if (await repository.EmailExistsAsync(request.Email, id, cancellationToken))
        {
            return Result.Failure<UserResponse>(UserErrors.EmailConflict);
        }

        user.Update(request.Username, request.Email, request.Role, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(UserResponse.FromUser(user));
    }

    public async Task<Result> SetStatusAsync(
        Guid id,
        UpdateUserStatusRequest request,
        CancellationToken cancellationToken)
    {
        User? user = await repository.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return Result.Failure(UserErrors.NotFound);
        }

        user.SetStatus(request.IsActive, timeProvider.GetUtcNow());
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
