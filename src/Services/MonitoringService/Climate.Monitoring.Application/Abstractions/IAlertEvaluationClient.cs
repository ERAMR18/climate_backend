using Climate.Monitoring.Domain.Readings;

namespace Climate.Monitoring.Application.Abstractions;

public interface IAlertEvaluationClient
{
    Task EvaluateAsync(SensorReading reading, CancellationToken cancellationToken);
}
