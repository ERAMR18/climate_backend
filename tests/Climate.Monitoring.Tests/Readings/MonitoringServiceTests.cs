using Climate.Contracts.Sensors;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Domain.Readings;
using Climate.SharedKernel.Results;
using Moq;
using ContractSensorType = Climate.Contracts.Sensors.SensorType;
using DomainSensorType = Climate.Monitoring.Domain.Readings.SensorType;

namespace Climate.Monitoring.Tests.Readings;

public sealed class MonitoringServiceTests
{
    private readonly Mock<IMonitoringRepository> _repository = new();
    private readonly Mock<ISensorCatalogClient> _sensorClient = new();
    private readonly Mock<ISimulatedValueGenerator> _generator = new();
    private readonly Mock<IAlertEvaluationClient> _alertClient = new();
    private readonly Mock<IRealtimePublisher> _realtimePublisher = new();

    [Fact]
    public async Task InactiveCatalogEntryCannotProduceManualOrSimulatedReadings()
    {
        var sensor = CreateSensor(ContractSensorType.Smoke, "%") with { IsActive = false };
        _sensorClient.Setup(x => x.GetActiveSensorsAsync(It.IsAny<CancellationToken>())).ReturnsAsync([sensor]);
        var result = await CreateService().CreateAsync(new CreateReadingRequest(sensor.Id, 80m), CancellationToken.None);
        Assert.True(result.IsFailure);
        await CreateService().GenerateSimulationBatchAsync(CancellationToken.None);
        _repository.Verify(x => x.AddAsync(It.IsAny<SensorReading>(), It.IsAny<CancellationToken>()), Times.Never);
        _alertClient.Verify(x => x.EvaluateAsync(It.IsAny<SensorReading>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreatePersistsReadingUsingCatalogMetadata()
    {
        SensorSummary sensor = CreateSensor(ContractSensorType.Temperature, "°C");
        _sensorClient.Setup(client => client.GetActiveSensorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([sensor]);
        SensorReading? persisted = null;
        _repository.Setup(repository => repository.AddAsync(It.IsAny<SensorReading>(), It.IsAny<CancellationToken>()))
            .Callback<SensorReading, CancellationToken>((reading, _) => persisted = reading)
            .Returns(Task.CompletedTask);

        Result<SensorReadingResponse> result = await CreateService().CreateAsync(
            new CreateReadingRequest(sensor.Id, 24.5m),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persisted);
        Assert.Equal(sensor.CommunityId, persisted.CommunityId);
        Assert.Equal("°C", persisted.Unit);
        Assert.Equal(24.5m, persisted.Value);
        _realtimePublisher.Verify(x=>x.PublishAsync(
            Climate.Contracts.Realtime.RealtimeEventNames.SensorReadingUpdated,
            It.IsAny<SensorReadingResponse>(),It.IsAny<CancellationToken>()),Times.Once);
    }

    [Fact]
    public async Task CreateRejectsInactiveOrUnknownSensor()
    {
        _sensorClient.Setup(client => client.GetActiveSensorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        Result<SensorReadingResponse> result = await CreateService().CreateAsync(
            new CreateReadingRequest(Guid.NewGuid(), 10m),
            CancellationToken.None);

        Assert.Equal(MonitoringErrors.SensorNotFound, result.Error);
        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<SensorReading>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SimulationBatchCreatesOneReadingPerActiveSensor()
    {
        SensorSummary temperature = CreateSensor(ContractSensorType.Temperature, "°C");
        SensorSummary humidity = CreateSensor(ContractSensorType.Humidity, "%");
        _sensorClient.Setup(client => client.GetActiveSensorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([temperature, humidity]);
        _generator.Setup(generator => generator.NextValue(ContractSensorType.Temperature)).Returns(22m);
        _generator.Setup(generator => generator.NextValue(ContractSensorType.Humidity)).Returns(70m);

        await CreateService().GenerateSimulationBatchAsync(CancellationToken.None);

        _repository.Verify(
            repository => repository.AddAsync(It.IsAny<SensorReading>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
        _repository.Verify(repository => repository.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        _realtimePublisher.Verify(x=>x.PublishAsync(
            Climate.Contracts.Realtime.RealtimeEventNames.SensorReadingUpdated,
            It.IsAny<SensorReadingResponse>(),It.IsAny<CancellationToken>()),Times.Exactly(2));
    }

    [Fact]
    public async Task HistoryRejectsInvertedPeriod()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;

        Result<IReadOnlyCollection<SensorReadingResponse>> result = await CreateService().GetHistoryAsync(
            Guid.NewGuid(),
            new HistoryFilter(null, now, now.AddHours(-1)),
            CancellationToken.None);

        Assert.Equal(MonitoringErrors.InvalidPeriod, result.Error);
    }

    [Fact]
    public async Task ChartAggregatesValuesByConfiguredBucket()
    {
        Guid sensorId = Guid.NewGuid();
        DateTimeOffset start = new(2026, 8, 17, 10, 1, 0, TimeSpan.Zero);
        SensorReading[] readings =
        [
            CreateReading(sensorId, 20m, start),
            CreateReading(sensorId, 24m, start.AddMinutes(2)),
            CreateReading(sensorId, 30m, start.AddMinutes(5))
        ];
        _repository.Setup(repository => repository.GetHistoryAsync(
                sensorId,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(readings);

        Result<SensorChartResponse> result = await CreateService().GetChartAsync(
            sensorId,
            null,
            null,
            "5m",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value.Data.Count);
        Assert.Equal(22m, result.Value.Data.First().Value);
    }

    [Fact]
    public async Task ChartReturnsEmptyDataWhenSensorHasNoReadings()
    {
        SensorSummary sensor = CreateSensor(ContractSensorType.Humidity, "%");
        _repository.Setup(repository => repository.GetHistoryAsync(
                sensor.Id,
                null,
                null,
                null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _sensorClient.Setup(client => client.GetActiveSensorsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([sensor]);

        Result<SensorChartResponse> result = await CreateService().GetChartAsync(
            sensor.Id,
            null,
            null,
            "5m",
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Data);
        Assert.Equal("%", result.Value.Unit);
    }

    private MonitoringService CreateService() =>
        new(
            _repository.Object,
            _sensorClient.Object,
            _alertClient.Object,
            _realtimePublisher.Object,
            _generator.Object,
            new CreateReadingRequestValidator(),
            TimeProvider.System);

    private static SensorSummary CreateSensor(ContractSensorType type, string unit) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Sensor", Guid.NewGuid().ToString("N"), type, unit, true);

    private static SensorReading CreateReading(Guid sensorId, decimal value, DateTimeOffset recordedAt) =>
        SensorReading.Create(
            Guid.NewGuid(),
            sensorId,
            Guid.NewGuid(),
            DomainSensorType.Temperature,
            value,
            "°C",
            recordedAt);
}
