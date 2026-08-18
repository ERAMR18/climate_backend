namespace Climate.Alerts.Domain.Alerts;

public sealed class ClimateAlert
{
    private ClimateAlert()
    {
    }

    private ClimateAlert(
        Guid id,
        Guid sensorId,
        Guid communityId,
        RiskType alertType,
        AlertLevel level,
        string title,
        string description,
        decimal sensorValue,
        decimal thresholdValue,
        DateTimeOffset generatedAt)
    {
        Id = id;
        SensorId = sensorId;
        CommunityId = communityId;
        AlertType = alertType;
        UpdateAssessment(level, title, description, sensorValue, thresholdValue, generatedAt);
    }

    public Guid Id { get; private set; }
    public Guid SensorId { get; private set; }
    public Guid CommunityId { get; private set; }
    public RiskType AlertType { get; private set; }
    public AlertLevel Level { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public decimal SensorValue { get; private set; }
    public decimal ThresholdValue { get; private set; }
    public DateTimeOffset GeneratedAt { get; private set; }
    public bool IsActive { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static ClimateAlert Create(
        Guid id,
        Guid sensorId,
        Guid communityId,
        RiskType alertType,
        AlertLevel level,
        string title,
        string description,
        decimal sensorValue,
        decimal thresholdValue,
        DateTimeOffset generatedAt) =>
        new(id, sensorId, communityId, alertType, level, title, description, sensorValue, thresholdValue, generatedAt);

    public void UpdateAssessment(
        AlertLevel level,
        string title,
        string description,
        decimal sensorValue,
        decimal thresholdValue,
        DateTimeOffset generatedAt)
    {
        Level = level;
        Title = title;
        Description = description;
        SensorValue = sensorValue;
        ThresholdValue = thresholdValue;
        GeneratedAt = generatedAt;
        IsActive = true;
        ResolvedAt = null;
    }

    public void Resolve(DateTimeOffset resolvedAt)
    {
        IsActive = false;
        ResolvedAt = resolvedAt;
    }
}
