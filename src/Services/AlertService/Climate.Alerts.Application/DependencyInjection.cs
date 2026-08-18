using Climate.Alerts.Application.Alerts;
using Climate.Alerts.Application.Risk;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Alerts.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddAlertsApplication(this IServiceCollection services)
    {
        services.AddScoped<IAlertService, AlertService>();
        services.AddScoped<IRiskEvaluationService, RiskEvaluationService>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
