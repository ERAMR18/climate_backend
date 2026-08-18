using Climate.Identity.Application.Users;

namespace Climate.Identity.Tests.Users;

public sealed class RequestValidatorTests
{
    [Theory]
    [InlineData("short")]
    [InlineData("alllowercase123")]
    [InlineData("ALLUPPERCASE123")]
    [InlineData("NoNumberPassword")]
    public async Task RegisterValidatorRejectsWeakPassword(string password)
    {
        var validator = new RegisterRequestValidator();

        var result = await validator.ValidateAsync(
            new RegisterRequest("valid.user", "valid@example.com", password),
            CancellationToken.None);

        Assert.False(result.IsValid);
    }

    [Fact]
    public async Task UpdateValidatorRejectsUnknownRole()
    {
        var validator = new UpdateUserRequestValidator();

        var result = await validator.ValidateAsync(
            new UpdateUserRequest("valid.user", "valid@example.com", "SuperUser"),
            CancellationToken.None);

        Assert.False(result.IsValid);
    }
}
