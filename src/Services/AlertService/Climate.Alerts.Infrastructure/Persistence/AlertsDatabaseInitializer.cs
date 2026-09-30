using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Climate.Alerts.Infrastructure.Persistence;

public static class AlertsDatabaseInitializer
{
    public static async Task InitializeAlertsDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using AsyncServiceScope scope = serviceProvider.CreateAsyncScope();
        AlertsDbContext dbContext = scope.ServiceProvider.GetRequiredService<AlertsDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        if (!await dbContext.AlertRules.AnyAsync(cancellationToken))
        {
            var options = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Climate.Alerts.Infrastructure.Risk.RiskRulesOptions>>().Value;
            foreach (var rule in options.Rules)
            foreach (var pair in new[] { (Climate.Alerts.Domain.Alerts.AlertLevel.Yellow, rule.Yellow), (Climate.Alerts.Domain.Alerts.AlertLevel.Orange, rule.Orange), (Climate.Alerts.Domain.Alerts.AlertLevel.Red, rule.Red) })
            {
                bool increasing = rule.Direction == Climate.Alerts.Application.Risk.ThresholdDirection.Increasing;
                dbContext.AlertRules.Add(new Climate.Alerts.Domain.Alerts.AlertRule {
                    Id = Guid.NewGuid(), Name = $"{rule.Title} ({pair.Item1})", SensorType = (int)rule.SensorType,
                    MinimumValue = increasing ? null : pair.Item2 + 0.0001m,
                    MaximumValue = increasing ? pair.Item2 - 0.0001m : null,
                    AlertLevel = pair.Item1, RiskType = rule.RiskType, Message = rule.Description,
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow });
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
