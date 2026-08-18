using Climate.Alerts.Application.Abstractions;
using Climate.Alerts.Application.Risk;
using Climate.Alerts.Infrastructure.Persistence;
using Climate.Alerts.Infrastructure.Risk;
using Climate.Alerts.Infrastructure.Clients;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Alerts.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAlertsInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        string connectionString = configuration.GetConnectionString("AlertDb")
            ?? throw new InvalidOperationException("Connection string 'AlertDb' is required.");
        services.AddDbContext<AlertsDbContext>(options => options.UseSqlServer(connectionString));
        services.AddHealthChecks().AddDbContextCheck<AlertsDbContext>("alert-database");

        services.AddOptions<RiskRulesOptions>()
            .Bind(configuration.GetSection(RiskRulesOptions.SectionName))
            .Validate(options => options.Rules.Count > 0, "At least one risk rule is required.")
            .Validate(options => options.Rules.All(IsValid), "Risk thresholds must be ordered for their direction.")
            .ValidateOnStart();
        services.AddScoped<IAlertRepository, AlertRepository>();
        services.AddOptions<EventServiceOptions>().Bind(configuration.GetSection(EventServiceOptions.SectionName))
            .Validate(x => Uri.TryCreate(x.BaseUrl, UriKind.Absolute, out _), "Event Service base URL is required.")
            .Validate(x => x.ApiKey.Length >= 32, "Event Service API key must contain at least 32 characters.").ValidateOnStart();
        services.AddHttpClient<IEventHistoryClient, EventHistoryClient>((provider, client) =>
            client.BaseAddress = new Uri(provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<EventServiceOptions>>().Value.BaseUrl));
        services.AddSingleton<IRiskRuleProvider, ConfiguredRiskRuleProvider>();
        return services;
    }

    private static bool IsValid(RiskRuleOptions rule) =>
        !string.IsNullOrWhiteSpace(rule.Title) &&
        !string.IsNullOrWhiteSpace(rule.Description) &&
        (rule.Direction == ThresholdDirection.Increasing
            ? rule.Yellow <= rule.Orange && rule.Orange <= rule.Red
            : rule.Yellow >= rule.Orange && rule.Orange >= rule.Red);
}
