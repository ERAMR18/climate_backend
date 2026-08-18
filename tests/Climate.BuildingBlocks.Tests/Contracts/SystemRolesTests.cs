using Climate.Contracts.Identity;

namespace Climate.BuildingBlocks.Tests.Contracts;

public sealed class SystemRolesTests
{
    [Theory]
    [InlineData("Administrator")]
    [InlineData("operator")]
    [InlineData("VIEWER")]
    public void IsDefinedRecognizesRoleIgnoringCase(string role)
    {
        Assert.True(SystemRoles.IsDefined(role));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Unknown")]
    public void IsDefinedRejectsUnknownRole(string? role)
    {
        Assert.False(SystemRoles.IsDefined(role));
    }
}
