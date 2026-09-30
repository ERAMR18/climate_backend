using Climate.Alerts.Application.Risk;
using Climate.Alerts.Infrastructure.Persistence;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Sensors;
using Microsoft.EntityFrameworkCore;
namespace Climate.Alerts.Infrastructure.Risk;
public sealed class DatabaseRiskEvaluationService(AlertsDbContext db) : IRiskEvaluationService
{
    public IReadOnlyCollection<RiskAssessment> Evaluate(SensorType sensorType, decimal value) =>
        throw new NotSupportedException("Use asynchronous database evaluation.");
    public async Task<IReadOnlyCollection<RiskAssessment>> EvaluateAsync(SensorType sensorType, decimal value, CancellationToken token)
    {
        var rules = await db.AlertRules.AsNoTracking().Where(x => x.IsActive && x.SensorType == (int)sensorType).ToArrayAsync(token);
        return Assess(rules, value);
    }
    public static IReadOnlyCollection<RiskAssessment> Assess(IEnumerable<AlertRule> rules, decimal value) =>
        rules.Where(x => x.IsActive && x.IsViolated(value) && x.AlertLevel != AlertLevel.Green)
            .GroupBy(x => x.RiskType)
            .Select(g => g.OrderByDescending(x => x.AlertLevel).ThenBy(x => x.Id).First())
            .Select(x => new RiskAssessment(x.RiskType, x.AlertLevel,
                x.MinimumValue.HasValue && value < x.MinimumValue ? x.MinimumValue.Value : x.MaximumValue!.Value,
                x.Name, x.Message, x.Id, x.MinimumValue, x.MaximumValue)).ToArray();
}
