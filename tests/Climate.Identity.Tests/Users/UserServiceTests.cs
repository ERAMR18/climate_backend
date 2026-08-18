using Climate.Contracts.Identity;
using Climate.Identity.Application.Abstractions;
using Climate.Identity.Application.Users;
using Climate.Identity.Domain.Users;
using Climate.SharedKernel.Results;
using Moq;

namespace Climate.Identity.Tests.Users;

public sealed class UserServiceTests
{
    private readonly Mock<IUserRepository> _repository = new();
    private readonly Mock<IPasswordService> _passwordService = new();
    private readonly Mock<IJwtTokenGenerator> _tokenGenerator = new();

    [Fact]
    public async Task LoginReturnsTokenForValidActiveUser()
    {
        User user = CreateUser();
        var expectedToken = new AccessToken("signed-token", DateTimeOffset.UtcNow.AddHours(1));
        _repository.Setup(repository => repository.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordService.Setup(service => service.Verify(user, user.PasswordHash, "ValidPassword1"))
            .Returns(PasswordCheckResult.Success);
        _tokenGenerator.Setup(generator => generator.Generate(user)).Returns(expectedToken);

        UserService service = CreateService();
        Result<LoginResponse> result = await service.LoginAsync(
            new LoginRequest("admin", "ValidPassword1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedToken.Value, result.Value.AccessToken);
        Assert.Equal(user.Id, result.Value.User.Id);
    }

    [Fact]
    public async Task LoginRejectsInvalidPasswordWithoutGeneratingToken()
    {
        User user = CreateUser();
        _repository.Setup(repository => repository.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordService.Setup(service => service.Verify(user, user.PasswordHash, "WrongPassword1"))
            .Returns(PasswordCheckResult.Failed);

        Result<LoginResponse> result = await CreateService().LoginAsync(
            new LoginRequest("admin", "WrongPassword1"),
            CancellationToken.None);

        Assert.True(result.IsFailure);
        Assert.Equal(UserErrors.InvalidCredentials, result.Error);
        _tokenGenerator.Verify(generator => generator.Generate(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task LoginRejectsInactiveUser()
    {
        User user = CreateUser();
        user.SetStatus(false, DateTimeOffset.UtcNow);
        _repository.Setup(repository => repository.GetByLoginAsync("admin", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);
        _passwordService.Setup(service => service.Verify(user, user.PasswordHash, "ValidPassword1"))
            .Returns(PasswordCheckResult.Success);

        Result<LoginResponse> result = await CreateService().LoginAsync(
            new LoginRequest("admin", "ValidPassword1"),
            CancellationToken.None);

        Assert.Equal(UserErrors.Inactive, result.Error);
    }

    [Fact]
    public async Task RegisterHashesPasswordAndAssignsViewerRole()
    {
        _passwordService.Setup(service => service.Hash(It.IsAny<User>(), "ValidPassword1"))
            .Returns("secure-password-hash");
        User? persistedUser = null;
        _repository.Setup(repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => persistedUser = user)
            .Returns(Task.CompletedTask);

        Result<UserResponse> result = await CreateService().RegisterAsync(
            new RegisterRequest("new.user", "new@example.com", "ValidPassword1"),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persistedUser);
        Assert.Equal("secure-password-hash", persistedUser.PasswordHash);
        Assert.Equal(SystemRoles.Viewer, persistedUser.Role);
        Assert.DoesNotContain("ValidPassword1", persistedUser.PasswordHash, StringComparison.Ordinal);
        _repository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RegisterRejectsDuplicateEmail()
    {
        _repository.Setup(repository => repository.EmailExistsAsync(
                "existing@example.com",
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        Result<UserResponse> result = await CreateService().RegisterAsync(
            new RegisterRequest("new.user", "existing@example.com", "ValidPassword1"),
            CancellationToken.None);

        Assert.Equal(UserErrors.EmailConflict, result.Error);
        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SetStatusDeactivatesExistingUser()
    {
        User user = CreateUser();
        _repository.Setup(repository => repository.GetByIdAsync(user.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        Result result = await CreateService().SetStatusAsync(
            user.Id,
            new UpdateUserStatusRequest(false),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(user.IsActive);
    }

    private UserService CreateService() =>
        new(
            _repository.Object,
            _passwordService.Object,
            _tokenGenerator.Object,
            new RegisterRequestValidator(),
            new LoginRequestValidator(),
            new UpdateUserRequestValidator(),
            TimeProvider.System);

    private static User CreateUser() =>
        User.Create(
            Guid.NewGuid(),
            "admin",
            "admin@example.com",
            "stored-password-hash",
            SystemRoles.Administrator,
            DateTimeOffset.UtcNow);
}
