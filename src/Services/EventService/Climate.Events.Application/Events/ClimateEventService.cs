using Climate.Events.Application.Abstractions;
using Climate.Events.Domain.Events;
using Climate.SharedKernel.Results;

namespace Climate.Events.Application.Events;

public sealed class ClimateEventService(IClimateEventRepository repository) : IClimateEventService
{
    public async Task<IReadOnlyCollection<EventResponse>> ListAsync(EventFilter filter, CancellationToken cancellationToken) =>
        (await repository.ListAsync(filter.RiskType, filter.AlertLevel, filter.SensorId, filter.CommunityId,
            filter.From, filter.To, cancellationToken)).Select(EventResponse.FromEntity).ToArray();

    public async Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        ClimateEvent? item = await repository.GetByIdAsync(id, cancellationToken);
        return item is null ? Result.Failure<EventResponse>(EventErrors.NotFound) : Result.Success(EventResponse.FromEntity(item));
    }

    public async Task<Result<EventResponse>> RecordAsync(RecordClimateEventRequest request, CancellationToken cancellationToken)
    {
        if (request.EventId == Guid.Empty || request.AlertId == Guid.Empty || request.SensorId == Guid.Empty ||
            request.CommunityId == Guid.Empty || string.IsNullOrWhiteSpace(request.Description))
            return Result.Failure<EventResponse>(EventErrors.Invalid);

        ClimateEvent? item = await repository.GetByAlertIdAsync(request.AlertId, cancellationToken);
        if (item is null)
        {
            item = ClimateEvent.Create(request.EventId, request.AlertId, request.SensorId, request.CommunityId,
                request.RiskType, request.AlertLevel, request.Description, request.OccurredAt, request.ResolvedAt);
            await repository.AddAsync(item, cancellationToken);
        }
        else item.Update(request.AlertLevel, request.Description, request.ResolvedAt);
        await repository.SaveChangesAsync(cancellationToken);
        return Result.Success(EventResponse.FromEntity(item));
    }
}

public static class EventErrors
{
    public static readonly ApplicationError NotFound = ApplicationError.NotFound("event.not_found", "The requested event does not exist.");
    public static readonly ApplicationError Invalid = ApplicationError.Validation("event.invalid", "Identifiers and description are required.");
}
