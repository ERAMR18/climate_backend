namespace Climate.Alerts.Domain.Alerts;

public sealed class AlertRule
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SensorType { get; set; }
    public decimal? MinimumValue { get; set; }
    public decimal? MaximumValue { get; set; }
    public AlertLevel AlertLevel { get; set; }
    public RiskType RiskType { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    // The bounds describe the acceptable interval; a value outside it triggers this rule.
    public bool IsViolated(decimal value) =>
        (MinimumValue.HasValue && value < MinimumValue) ||
        (MaximumValue.HasValue && value > MaximumValue);
}
