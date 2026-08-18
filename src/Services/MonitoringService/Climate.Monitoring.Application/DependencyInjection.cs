using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Application.Simulation;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Monitoring.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddMonitoringApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssemblyContaining<CreateReadingRequestValidator>();
        services.AddScoped<IMonitoringService, MonitoringService>();
        services.AddScoped<ISimulationService, SimulationService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
