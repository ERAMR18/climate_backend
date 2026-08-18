using Climate.Sensors.Application.Communities;
using Climate.Sensors.Application.Sensors;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Sensors.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddSensorsApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateSensorRequestValidator>();
        services.AddScoped<ISensorService, SensorService>();
        services.AddScoped<ICommunityService, CommunityService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
