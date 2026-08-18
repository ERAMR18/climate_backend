using Climate.Events.Application.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Events.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddEventsApplication(this IServiceCollection services) =>
        services.AddScoped<IClimateEventService, ClimateEventService>();
}
