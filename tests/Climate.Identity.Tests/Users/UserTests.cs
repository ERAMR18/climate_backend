using Climate.Contracts.Identity;
using Climate.Identity.Domain.Users;

namespace Climate.Identity.Tests.Users;

public sealed class UserTests
{
    [Fact]
    public void CreateNormalizesLookupValuesAndStartsActive()
    {
        User user = User.Create(
            Guid.NewGuid(),
            "  Demo.User  ",
            "  Demo@Example.com ",
            "hash",
            SystemRoles.Viewer,
            DateTimeOffset.UtcNow);

        Assert.Equal("Demo.User", user.Username);
        Assert.Equal("DEMO.USER", user.NormalizedUsername);
        Assert.Equal("Demo@Example.com", user.Email);
        Assert.Equal("DEMO@EXAMPLE.COM", user.NormalizedEmail);
        Assert.True(user.IsActive);
    }

    [Fact]
    public void UpdateRenormalizesIdentityAndRole()
    {
        User user=User.Create(Guid.NewGuid(),"old","old@example.com","hash","Viewer",DateTimeOffset.UtcNow);
        DateTimeOffset updated=DateTimeOffset.UtcNow.AddMinutes(1); user.Update(" NewUser "," NEW@example.com ","Operator",updated);
        Assert.Equal("NewUser",user.Username); Assert.Equal("NEWUSER",user.NormalizedUsername); Assert.Equal("NEW@EXAMPLE.COM",user.NormalizedEmail); Assert.Equal("Operator",user.Role); Assert.Equal(updated,user.UpdatedAt);
    }

    [Fact]
    public void PasswordAndStatusChangesUpdateTimestamp()
    {
        User user=User.Create(Guid.NewGuid(),"user","u@example.com","old","Viewer",DateTimeOffset.UtcNow);
        DateTimeOffset changed=DateTimeOffset.UtcNow.AddMinutes(1); user.SetPasswordHash("new",changed); user.SetStatus(false,changed.AddMinutes(1));
        Assert.Equal("new",user.PasswordHash); Assert.False(user.IsActive); Assert.Equal(changed.AddMinutes(1),user.UpdatedAt);
    }
}
