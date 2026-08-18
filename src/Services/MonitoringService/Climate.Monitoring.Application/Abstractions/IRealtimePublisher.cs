namespace Climate.Monitoring.Application.Abstractions;
public interface IRealtimePublisher
{
    Task PublishAsync(string eventName, object payload, CancellationToken cancellationToken);
}
