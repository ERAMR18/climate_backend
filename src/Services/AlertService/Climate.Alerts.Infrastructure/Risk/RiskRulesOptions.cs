using Climate.Alerts.Application.Risk;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Sensors;

namespace Climate.Alerts.Infrastructure.Risk;

public sealed class RiskRulesOptions
{
    public const string SectionName = "RiskRules";
    public List<RiskRuleOptions> Rules { get; init; } = [];
}

public sealed class RiskRuleOptions
{
    public SensorType SensorType { get; init; }
    public RiskType RiskType { get; init; }
    public ThresholdDirection Direction { get; init; }
    public decimal Yellow { get; init; }
    public decimal Orange { get; init; }
    public decimal Red { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
