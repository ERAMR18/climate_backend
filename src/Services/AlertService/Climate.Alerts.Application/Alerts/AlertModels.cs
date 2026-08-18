using Climate.Alerts.Domain.Alerts;

namespace Climate.Alerts.Application.Alerts;

public sealed record AlertFilter(
    RiskType? RiskType,
    AlertLevel? AlertLevel,
    Guid? SensorId,
    Guid? CommunityId,
    bool? IsActive);

public sealed record AlertResponse(
    Guid Id,
    Guid SensorId,
    Guid CommunityId,
    RiskType AlertType,
    AlertLevel Level,
    string Title,
    string Description,
    decimal SensorValue,
    decimal ThresholdValue,
    DateTimeOffset GeneratedAt,
    bool IsActive,
    DateTimeOffset? ResolvedAt)
{
    public static AlertResponse FromEntity(ClimateAlert alert) =>
        new(
            alert.Id,
            alert.SensorId,
            alert.CommunityId,
            alert.AlertType,
            alert.Level,
            alert.Title,
            alert.Description,
            alert.SensorValue,
            alert.ThresholdValue,
            alert.GeneratedAt,
            alert.IsActive,
            alert.ResolvedAt);
}
