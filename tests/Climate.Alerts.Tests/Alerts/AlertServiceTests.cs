using Climate.Alerts.Application.Abstractions;
using Climate.Alerts.Application.Alerts;
using Climate.Alerts.Application.Risk;
using Climate.Alerts.Domain.Alerts;
using Climate.Contracts.Monitoring;
using Climate.Contracts.Sensors;
using Climate.SharedKernel.Results;
using Moq;

namespace Climate.Alerts.Tests.Alerts;

public sealed class AlertServiceTests
{
    private readonly Mock<IAlertRepository> _repository = new();
    private readonly Mock<IRiskEvaluationService> _evaluation = new();
    private readonly Mock<IEventHistoryClient> _eventHistory = new();
    private readonly Mock<ISensorActivityClient> _sensorActivity = new();

    public AlertServiceTests() => _sensorActivity.Setup(x => x.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

    [Fact]
    public async Task InactiveSensorDoesNotEvaluateRulesOrCreateAlerts()
    {
        _sensorActivity.Setup(x => x.IsActiveAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var result = await CreateService().EvaluateAsync(CreateReading(99m), CancellationToken.None);
        Assert.Empty(result.Value);
        _evaluation.Verify(x => x.Evaluate(It.IsAny<SensorType>(), It.IsAny<decimal>()), Times.Never);
        _repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task EvaluateCreatesAlertForNonGreenAssessment()
    {
        SensorReadingRecorded reading = CreateReading(7.5m);
        _evaluation.Setup(value => value.EvaluateAsync(reading.SensorType, reading.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateAssessment(AlertLevel.Red)]);
        ClimateAlert? persisted = null;
        _repository.Setup(value => value.AddAsync(It.IsAny<ClimateAlert>(), It.IsAny<CancellationToken>()))
            .Callback<ClimateAlert, CancellationToken>((alert, _) => persisted = alert)
            .Returns(Task.CompletedTask);

        Result<IReadOnlyCollection<AlertResponse>> result = await CreateService().EvaluateAsync(
            reading,
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(persisted);
        Assert.Equal(AlertLevel.Red, persisted.Level);
        Assert.Equal(reading.SensorId, persisted.SensorId);
        Assert.True(persisted.IsActive);
    }

    [Fact]
    public async Task EvaluateUpdatesExistingAlertInsteadOfCreatingDuplicate()
    {
        SensorReadingRecorded reading = CreateReading(6m);
        ClimateAlert existing = CreateAlert(AlertLevel.Yellow);
        _evaluation.Setup(value => value.EvaluateAsync(reading.SensorType, reading.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateAssessment(AlertLevel.Orange)]);
        _repository.Setup(value => value.GetActiveAsync(
                reading.SensorId,
                RiskType.Flood,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateService().EvaluateAsync(reading, CancellationToken.None);

        Assert.Equal(AlertLevel.Orange, existing.Level);
        Assert.Equal(reading.Value, existing.SensorValue);
        _repository.Verify(
            value => value.AddAsync(It.IsAny<ClimateAlert>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task GreenAssessmentResolvesExistingAlert()
    {
        SensorReadingRecorded reading = CreateReading(2m);
        ClimateAlert existing = CreateAlert(AlertLevel.Yellow);
        _evaluation.Setup(value => value.EvaluateAsync(reading.SensorType, reading.Value, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateAssessment(AlertLevel.Green)]);
        _repository.Setup(value => value.GetActiveAsync(
                reading.SensorId,
                RiskType.Flood,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        await CreateService().EvaluateAsync(reading, CancellationToken.None);

        Assert.False(existing.IsActive);
        Assert.Equal(reading.RecordedAt, existing.ResolvedAt);
    }

    [Fact]
    public async Task ResolveReturnsNotFoundForUnknownAlert()
    {
        Result result = await CreateService().ResolveAsync(Guid.NewGuid(), CancellationToken.None);
        Assert.Equal(AlertErrors.NotFound, result.Error);
    }

    private AlertService CreateService() =>
        new(_repository.Object, _evaluation.Object, _eventHistory.Object, TimeProvider.System, _sensorActivity.Object);

    private static RiskAssessment CreateAssessment(AlertLevel level) =>
        new(RiskType.Flood, level, 7m, "Flood risk", "Configured threshold exceeded.");

    private static SensorReadingRecorded CreateReading(decimal value)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        return new SensorReadingRecorded(
            Guid.NewGuid(),
            now,
            "correlation-id",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SensorType.WaterLevel,
            value,
            "m",
            now);
    }

    private static ClimateAlert CreateAlert(AlertLevel level) =>
        ClimateAlert.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RiskType.Flood,
            level,
            "Flood risk",
            "Configured threshold exceeded.",
            4m,
            4m,
            DateTimeOffset.UtcNow);
}
