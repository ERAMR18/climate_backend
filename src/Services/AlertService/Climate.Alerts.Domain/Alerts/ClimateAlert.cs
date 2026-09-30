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
    public Guid? RuleId { get; private set; }
    public string? RuleName { get; private set; }
    public decimal? MinimumValueSnapshot { get; private set; }
    public decimal? MaximumValueSnapshot { get; private set; }
    public AlertStatus Status { get; private set; }
    public Guid? AttendedByUserId { get; private set; }
    public DateTimeOffset? AttendedAt { get; private set; }
    public Guid? ClosedByUserId { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public byte[] Version { get; private set; } = [];

    public void SetRule(Guid? id, string? name, decimal? minimum, decimal? maximum)
    {
        RuleId = id; RuleName = name; MinimumValueSnapshot = minimum; MaximumValueSnapshot = maximum;
    }

    public bool Attend(Guid userId, DateTimeOffset now)
    {
        if (Status != AlertStatus.Active || userId == Guid.Empty) return false;
        Status = AlertStatus.Attended; AttendedByUserId = userId; AttendedAt = now;
        return true;
    }

    public bool Close(Guid userId, DateTimeOffset now)
    {
        if (Status != AlertStatus.Attended || userId == Guid.Empty) return false;
        Status = AlertStatus.Closed; ClosedByUserId = userId; ClosedAt = now;
        IsActive = false; ResolvedAt = now;
        return true;
    }

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
        Status = AlertStatus.Closed;
        ClosedAt = resolvedAt;
        IsActive = false;
        ResolvedAt = resolvedAt;
    }
}
