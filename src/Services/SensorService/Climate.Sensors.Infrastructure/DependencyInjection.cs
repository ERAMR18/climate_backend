using Climate.Sensors.Application.Abstractions;
using Climate.Sensors.Infrastructure.Persistence;
using Climate.Sensors.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Sensors.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddSensorsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("SensorDb")
            ?? throw new InvalidOperationException("Connection string 'SensorDb' is required.");

        services.AddDbContext<SensorsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<SensorsDbContext>("sensor-database");
        services.AddOptions<DemoSeedOptions>().Bind(configuration.GetSection(DemoSeedOptions.SectionName));
        services.AddScoped<ISensorCatalogRepository, SensorCatalogRepository>();
        services.AddHttpContextAccessor();
        services.AddSingleton(new CommunityDirectoryOptions(configuration.GetValue<bool>("CommunityService:Enabled"),
            configuration["InternalApi:ApiKey"] ?? string.Empty));
        services.AddHttpClient<CommunityDirectory>(client =>
        {
            client.BaseAddress = new Uri(configuration["CommunityService:BaseUrl"] ?? "http://localhost:5107/");
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        return services;
    }
}
