using Climate.Events.Application.Abstractions;
using Climate.Events.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Events.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddEventsInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("EventDb") ?? throw new InvalidOperationException("Connection string 'EventDb' is required.");
        services.AddDbContext<EventsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<EventsDbContext>("event-database");
        services.AddScoped<IClimateEventRepository, ClimateEventRepository>();
        return services;
    }
}
