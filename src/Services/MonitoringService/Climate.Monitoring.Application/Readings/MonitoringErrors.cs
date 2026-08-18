using Climate.SharedKernel.Results;

namespace Climate.Monitoring.Application.Readings;

public static class MonitoringErrors
{
    public static readonly ApplicationError SensorNotFound = ApplicationError.NotFound(
        "monitoring.sensor_not_found",
        "The sensor does not exist or is inactive.");

    public static readonly ApplicationError ReadingNotFound = ApplicationError.NotFound(
        "monitoring.reading_not_found",
        "No reading exists for the requested sensor.");

    public static readonly ApplicationError InvalidPeriod = ApplicationError.Validation(
        "monitoring.invalid_period",
        "The start of the period must be earlier than or equal to its end.");

    public static readonly ApplicationError InvalidInterval = ApplicationError.Validation(
        "monitoring.invalid_interval",
        "Interval must be one of: 1m, 5m, 15m, 1h.");
}
