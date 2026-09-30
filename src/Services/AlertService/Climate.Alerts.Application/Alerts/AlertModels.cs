using Climate.Alerts.Domain.Alerts;

namespace Climate.Alerts.Application.Alerts;

public sealed record AlertFilter(
    RiskType? RiskType,
    AlertLevel? AlertLevel,
    Guid? SensorId,
    Guid? CommunityId,
    bool? IsActive, DateTimeOffset? From = null, DateTimeOffset? To = null, AlertStatus? Status = null);

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
    DateTimeOffset? ResolvedAt, AlertStatus Status = AlertStatus.Active,
    Guid? AttendedByUserId = null, DateTimeOffset? AttendedAt = null,
    Guid? ClosedByUserId = null, DateTimeOffset? ClosedAt = null,
    Guid? RuleId = null, string? RuleName = null, decimal? MinimumValueSnapshot = null, decimal? MaximumValueSnapshot = null)
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
            alert.ResolvedAt, alert.Status, alert.AttendedByUserId, alert.AttendedAt,
            alert.ClosedByUserId, alert.ClosedAt, alert.RuleId, alert.RuleName, alert.MinimumValueSnapshot, alert.MaximumValueSnapshot);
}
