using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Sensors;

namespace Climate.Alerts.Application.Risk;

public sealed class RiskEvaluationService(IRiskRuleProvider ruleProvider) : IRiskEvaluationService
{
    public IReadOnlyCollection<RiskAssessment> Evaluate(SensorType sensorType, decimal value) =>
        ruleProvider.GetRules(sensorType)
            .Select(rule => EvaluateRule(rule, value))
            .ToArray();

    private static RiskAssessment EvaluateRule(RiskRule rule, decimal value)
    {
        (AlertLevel level, decimal threshold) = rule.Direction switch
        {
            ThresholdDirection.Increasing => EvaluateIncreasing(rule, value),
            ThresholdDirection.Decreasing => EvaluateDecreasing(rule, value),
            _ => throw new ArgumentOutOfRangeException(nameof(rule), rule.Direction, "Unsupported direction.")
        };
        return new RiskAssessment(rule.RiskType, level, threshold, rule.Title, rule.Description);
    }

    private static (AlertLevel Level, decimal Threshold) EvaluateIncreasing(RiskRule rule, decimal value) =>
        value >= rule.Red ? (AlertLevel.Red, rule.Red) :
        value >= rule.Orange ? (AlertLevel.Orange, rule.Orange) :
        value >= rule.Yellow ? (AlertLevel.Yellow, rule.Yellow) :
        (AlertLevel.Green, rule.Yellow);

    private static (AlertLevel Level, decimal Threshold) EvaluateDecreasing(RiskRule rule, decimal value) =>
        value <= rule.Red ? (AlertLevel.Red, rule.Red) :
        value <= rule.Orange ? (AlertLevel.Orange, rule.Orange) :
        value <= rule.Yellow ? (AlertLevel.Yellow, rule.Yellow) :
        (AlertLevel.Green, rule.Yellow);
}
