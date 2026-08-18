namespace Climate.Contracts.Sensors;

public sealed record SensorSummary(
    Guid Id,
    Guid CommunityId,
    string Name,
    string Code,
    SensorType Type,
    string Unit,
    bool IsActive);
