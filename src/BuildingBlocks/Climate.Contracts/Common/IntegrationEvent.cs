namespace Climate.Contracts.Common;

public abstract record IntegrationEvent(
    Guid EventId,
    DateTimeOffset OccurredAt,
    string CorrelationId);
