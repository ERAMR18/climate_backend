using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Infrastructure.Clients;
using Climate.Monitoring.Infrastructure.Persistence;
using Climate.Monitoring.Infrastructure.Simulation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Monitoring.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMonitoringInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("MonitoringDb")
            ?? throw new InvalidOperationException("Connection string 'MonitoringDb' is required.");

        services.AddDbContext<MonitoringDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<MonitoringDbContext>("monitoring-database");

        services.AddOptions<SimulationOptions>()
            .Bind(configuration.GetSection(SimulationOptions.SectionName))
            .Validate(options => options.IntervalSeconds is >= 1 and <= 3600, "Simulation interval must be between 1 and 3600 seconds.")
            .Validate(HasValidRanges, "Every simulation range must have minimum <= maximum and 0-4 decimal places.")
            .ValidateOnStart();
        services.AddOptions<SensorServiceOptions>()
            .Bind(configuration.GetSection(SensorServiceOptions.SectionName))
            .Validate(options => options.BaseUrl is not null && options.BaseUrl.IsAbsoluteUri, "Sensor Service URL is required.")
            .Validate(options => options.ApiKey.Length >= 32, "Sensor Service API key must contain at least 32 characters.")
            .ValidateOnStart();
        services.AddOptions<AlertServiceOptions>()
            .Bind(configuration.GetSection(AlertServiceOptions.SectionName))
            .Validate(options => options.BaseUrl is not null && options.BaseUrl.IsAbsoluteUri, "Alert Service URL is required.")
            .Validate(options => options.ApiKey.Length >= 32, "Alert Service API key must contain at least 32 characters.")
            .ValidateOnStart();

        SimulationOptions simulation = configuration.GetSection(SimulationOptions.SectionName).Get<SimulationOptions>() ?? new();
        services.AddSingleton<ISimulationControl>(new SimulationControl(simulation.Enabled));
        services.AddSingleton<ISimulatedValueGenerator, SimulatedValueGenerator>();
        services.AddScoped<IMonitoringRepository, MonitoringRepository>();
        services.AddHttpClient<ISensorCatalogClient, SensorCatalogClient>((provider, client) =>
            client.BaseAddress = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<SensorServiceOptions>>().Value.BaseUrl);
        services.AddHttpClient<IAlertEvaluationClient, AlertEvaluationClient>((provider, client) =>
            client.BaseAddress = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AlertServiceOptions>>().Value.BaseUrl);
        services.AddHostedService<ClimateSimulationWorker>();
        return services;
    }

    private static bool HasValidRanges(SimulationOptions options) =>
        IsValid(options.Temperature) &&
        IsValid(options.Humidity) &&
        IsValid(options.WindSpeed) &&
        IsValid(options.Rainfall) &&
        IsValid(options.WaterLevel);

    private static bool IsValid(SensorRangeOptions range) =>
        range.Minimum <= range.Maximum && range.DecimalPlaces is >= 0 and <= 4;
}
