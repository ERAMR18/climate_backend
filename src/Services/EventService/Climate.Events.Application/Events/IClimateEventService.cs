using Climate.SharedKernel.Results;

namespace Climate.Events.Application.Events;

public interface IClimateEventService
{
    Task<IReadOnlyCollection<EventResponse>> ListAsync(EventFilter filter, CancellationToken cancellationToken);
    Task<Result<EventResponse>> GetByIdAsync(Guid id, CancellationToken cancellationToken);
    Task<Result<EventResponse>> RecordAsync(RecordClimateEventRequest request, CancellationToken cancellationToken);
}
