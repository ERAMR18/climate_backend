using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Sensors;

namespace Climate.Alerts.Application.Risk;

public enum ThresholdDirection
{
    Increasing,
    Decreasing
}

public sealed record RiskRule(
    SensorType SensorType,
    RiskType RiskType,
    ThresholdDirection Direction,
    decimal Yellow,
    decimal Orange,
    decimal Red,
    string Title,
    string Description);

public sealed record RiskAssessment(
    RiskType RiskType,
    AlertLevel Level,
    decimal ThresholdValue,
    string Title,
    string Description);

public interface IRiskRuleProvider
{
    IReadOnlyCollection<RiskRule> GetRules(SensorType sensorType);
}

public interface IRiskEvaluationService
{
    IReadOnlyCollection<RiskAssessment> Evaluate(SensorType sensorType, decimal value);
}
