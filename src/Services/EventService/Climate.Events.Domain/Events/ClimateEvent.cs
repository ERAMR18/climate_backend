using Climate.Contracts.Alerts;

namespace Climate.Events.Domain.Events;

public sealed class ClimateEvent
{
    private ClimateEvent() { }

    public Guid Id { get; private set; }
    public Guid AlertId { get; private set; }
    public Guid SensorId { get; private set; }
    public Guid CommunityId { get; private set; }
    public RiskType RiskType { get; private set; }
    public AlertLevel AlertLevel { get; private set; }
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }

    public static ClimateEvent Create(Guid id, Guid alertId, Guid sensorId, Guid communityId,
        RiskType riskType, AlertLevel alertLevel, string description, DateTimeOffset occurredAt,
        DateTimeOffset? resolvedAt = null)
    {
        if (id == Guid.Empty || alertId == Guid.Empty || sensorId == Guid.Empty || communityId == Guid.Empty)
            throw new ArgumentException("Event identifiers cannot be empty.");
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        return new ClimateEvent { Id = id, AlertId = alertId, SensorId = sensorId, CommunityId = communityId,
            RiskType = riskType, AlertLevel = alertLevel, Description = description.Trim(), OccurredAt = occurredAt,
            ResolvedAt = resolvedAt };
    }

    public void Update(AlertLevel level, string description, DateTimeOffset? resolvedAt)
    {
        AlertLevel = level;
        Description = description.Trim();
        ResolvedAt = resolvedAt;
    }
}
