using Climate.Identity.Application.Users;
using Climate.Identity.Domain.Users;

namespace Climate.Identity.Application.Abstractions;

public interface IJwtTokenGenerator
{
    AccessToken Generate(User user);
}
