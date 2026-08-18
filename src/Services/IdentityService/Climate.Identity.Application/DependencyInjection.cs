using Climate.Identity.Application.Users;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Identity.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddIdentityApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        services.AddScoped<IUserService, UserService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
