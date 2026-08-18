using Climate.Contracts.Sensors;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Domain.Readings;
using Climate.SharedKernel.Results;
using FluentValidation;
using Climate.Contracts.Realtime;

namespace Climate.Monitoring.Application.Readings;

public sealed class MonitoringService(
    IMonitoringRepository repository,
    ISensorCatalogClient sensorClient,
    IAlertEvaluationClient alertClient,
    IRealtimePublisher realtimePublisher,
    ISimulatedValueGenerator valueGenerator,
    IValidator<CreateReadingRequest> validator,
    TimeProvider timeProvider) : IMonitoringService
{
    public async Task<IReadOnlyCollection<SensorReadingResponse>> GetCurrentAsync(CancellationToken cancellationToken) =>
        (await repository.GetCurrentAsync(cancellationToken)).Select(SensorReadingResponse.FromEntity).ToArray();

    public async Task<Result<SensorReadingResponse>> GetLatestAsync(
        Guid sensorId,
        CancellationToken cancellationToken)
    {
        SensorReading? reading = await repository.GetLatestAsync(sensorId, cancellationToken);
        return reading is null
            ? Result.Failure<SensorReadingResponse>(MonitoringErrors.ReadingNotFound)
            : Result.Success(SensorReadingResponse.FromEntity(reading));
    }

    public async Task<Result<IReadOnlyCollection<SensorReadingResponse>>> GetHistoryAsync(
        Guid sensorId,
        HistoryFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.From > filter.To)
        {
            return Result.Failure<IReadOnlyCollection<SensorReadingResponse>>(MonitoringErrors.InvalidPeriod);
        }

        IReadOnlyCollection<SensorReading> readings = await repository.GetHistoryAsync(
            sensorId,
            filter.CommunityId,
            filter.From,
            filter.To,
            cancellationToken);
        return Result.Success<IReadOnlyCollection<SensorReadingResponse>>(
            readings.Select(SensorReadingResponse.FromEntity).ToArray());
    }

    public async Task<Result<SensorReadingResponse>> CreateAsync(
        CreateReadingRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await validator.ValidateAsync(request, cancellationToken);
        if (!validation.IsValid)
        {
            return Result.Failure<SensorReadingResponse>(ApplicationError.Validation(
                "validation.failed",
                string.Join(" ", validation.Errors.Select(error => error.ErrorMessage).Distinct())));
        }

        SensorSummary? sensor = (await sensorClient.GetActiveSensorsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == request.SensorId);
        if (sensor is null)
        {
            return Result.Failure<SensorReadingResponse>(MonitoringErrors.SensorNotFound);
        }

        SensorReading reading = CreateReading(sensor, request.Value, request.RecordedAt ?? timeProvider.GetUtcNow());
        await repository.AddAsync(reading, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        await alertClient.EvaluateAsync(reading, cancellationToken);
        SensorReadingResponse response=SensorReadingResponse.FromEntity(reading);
        await realtimePublisher.PublishAsync(RealtimeEventNames.SensorReadingUpdated,response,cancellationToken);
        return Result.Success(response);
    }

    public async Task<Result<SensorChartResponse>> GetChartAsync(
        Guid sensorId,
        DateTimeOffset? from,
        DateTimeOffset? until,
        string interval,
        CancellationToken cancellationToken)
    {
        if (from > until)
        {
            return Result.Failure<SensorChartResponse>(MonitoringErrors.InvalidPeriod);
        }

        if (!TryGetInterval(interval, out TimeSpan bucketSize))
        {
            return Result.Failure<SensorChartResponse>(MonitoringErrors.InvalidInterval);
        }

        IReadOnlyCollection<SensorReading> readings = await repository.GetHistoryAsync(
            sensorId,
            null,
            from,
            until,
            cancellationToken);
        if (readings.Count == 0)
        {
            SensorSummary? sensor = (await sensorClient.GetActiveSensorsAsync(cancellationToken))
                .SingleOrDefault(item => item.Id == sensorId);
            return sensor is null
                ? Result.Failure<SensorChartResponse>(MonitoringErrors.SensorNotFound)
                : Result.Success(new SensorChartResponse(sensorId, sensor.Unit, []));
        }

        ChartPoint[] points = readings
            .GroupBy(reading => AlignToBucket(reading.RecordedAt, bucketSize))
            .OrderBy(group => group.Key)
            .Select(group => new ChartPoint(group.Key, decimal.Round(group.Average(item => item.Value), 3)))
            .ToArray();
        return Result.Success(new SensorChartResponse(sensorId, readings.First().Unit, points));
    }

    public async Task GenerateSimulationBatchAsync(CancellationToken cancellationToken)
    {
        IReadOnlyCollection<SensorSummary> sensors = await sensorClient.GetActiveSensorsAsync(cancellationToken);
        DateTimeOffset now = timeProvider.GetUtcNow();
        var readings = new List<SensorReading>(sensors.Count);
        foreach (SensorSummary sensor in sensors)
        {
            SensorReading reading = CreateReading(sensor, valueGenerator.NextValue(sensor.Type), now);
            readings.Add(reading);
            await repository.AddAsync(reading, cancellationToken);
        }

        if (sensors.Count > 0)
        {
            await repository.SaveChangesAsync(cancellationToken);
            foreach (SensorReading reading in readings)
            {
                await alertClient.EvaluateAsync(reading, cancellationToken);
                await realtimePublisher.PublishAsync(RealtimeEventNames.SensorReadingUpdated,SensorReadingResponse.FromEntity(reading),cancellationToken);
            }
        }
    }

    private static SensorReading CreateReading(SensorSummary sensor, decimal value, DateTimeOffset recordedAt) =>
        SensorReading.Create(
            Guid.NewGuid(),
            sensor.Id,
            sensor.CommunityId,
            (Climate.Monitoring.Domain.Readings.SensorType)sensor.Type,
            value,
            sensor.Unit,
            recordedAt);

    private static bool TryGetInterval(string interval, out TimeSpan value)
    {
        value = interval.ToLowerInvariant() switch
        {
            "1m" => TimeSpan.FromMinutes(1),
            "5m" => TimeSpan.FromMinutes(5),
            "15m" => TimeSpan.FromMinutes(15),
            "1h" => TimeSpan.FromHours(1),
            _ => TimeSpan.Zero
        };
        return value != TimeSpan.Zero;
    }

    private static DateTimeOffset AlignToBucket(DateTimeOffset timestamp, TimeSpan bucketSize)
    {
        long ticks = timestamp.UtcTicks - timestamp.UtcTicks % bucketSize.Ticks;
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }
}
