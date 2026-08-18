using Climate.Contracts.Alerts;
using Climate.Contracts.Monitoring;
using Climate.Contracts.Sensors;

namespace Climate.BuildingBlocks.Tests.Contracts;

public sealed class IntegrationContractTests
{
    [Fact]
    public void RealtimeEventCatalogContainsTheFourPublicEvents()
    {
        Assert.Equal(4,Climate.Contracts.Realtime.RealtimeEventNames.All.Count);
        Assert.Contains(Climate.Contracts.Realtime.RealtimeEventNames.AlertGenerated,Climate.Contracts.Realtime.RealtimeEventNames.All);
    }
    [Fact]
    public void SensorReadingRecordedPreservesEventMetadata()
    {
        Guid eventId = Guid.NewGuid();
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        var integrationEvent = new SensorReadingRecorded(
            eventId,
            timestamp,
            "correlation-id",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SensorType.Temperature,
            24.5m,
            "°C",
            timestamp);

        Assert.Equal(eventId, integrationEvent.EventId);
        Assert.Equal("correlation-id", integrationEvent.CorrelationId);
        Assert.Equal(SensorType.Temperature, integrationEvent.SensorType);
    }

    [Fact]
    public void AlertGeneratedUsesConfiguredRiskAndLevel()
    {
        DateTimeOffset timestamp = DateTimeOffset.UtcNow;

        var integrationEvent = new AlertGenerated(
            Guid.NewGuid(),
            timestamp,
            "correlation-id",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            RiskType.Flood,
            AlertLevel.Red,
            "Flood risk",
            "Configured threshold exceeded.",
            6.2m,
            6m,
            timestamp);

        Assert.Equal(RiskType.Flood, integrationEvent.RiskType);
        Assert.Equal(AlertLevel.Red, integrationEvent.Level);
    }
}
