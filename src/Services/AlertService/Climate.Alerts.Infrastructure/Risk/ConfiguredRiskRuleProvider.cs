using Climate.Alerts.Application.Risk;
using Climate.Contracts.Sensors;
using Microsoft.Extensions.Options;

namespace Climate.Alerts.Infrastructure.Risk;

internal sealed class ConfiguredRiskRuleProvider(IOptions<RiskRulesOptions> options) : IRiskRuleProvider
{
    private readonly IReadOnlyCollection<RiskRule> _rules = options.Value.Rules
        .Select(rule => new RiskRule(
            rule.SensorType,
            rule.RiskType,
            rule.Direction,
            rule.Yellow,
            rule.Orange,
            rule.Red,
            rule.Title,
            rule.Description))
        .ToArray();

    public IReadOnlyCollection<RiskRule> GetRules(SensorType sensorType) =>
        _rules.Where(rule => rule.SensorType == sensorType).ToArray();
}
