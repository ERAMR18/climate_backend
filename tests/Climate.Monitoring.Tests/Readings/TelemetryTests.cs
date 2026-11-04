using System.Security.Cryptography;
using System.Text;
using Climate.Contracts.Sensors;
using Climate.Monitoring.Api.Controllers;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Moq;

namespace Climate.Monitoring.Tests.Readings;

public sealed class TelemetryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UnknownAndInactiveSensorsCannotAcceptAuthenticatedTelemetry(bool inactive)
    {
        Guid sensorId = Guid.NewGuid();
        const string credential = "test-device-credential-32-characters-long";
        TelemetryOptions options = new() { Devices = [new() { DeviceId = "device", SensorId = sensorId,
            TokenSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(credential))) }] };
        Mock<ISensorCatalogClient> catalog = new();
        catalog.Setup(client => client.GetActiveSensorsAsync(default)).ReturnsAsync(inactive
            ? [new SensorSummary(sensorId, Guid.NewGuid(), "Temperature", "T1", SensorType.Temperature, "C", false)] : []);
        await using MonitoringDbContext db = new(new DbContextOptionsBuilder<MonitoringDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        TelemetryController controller = new(db, catalog.Object, Options.Create(options))
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        controller.Request.Headers["X-Device-Token"] = credential;
        IActionResult response = await controller.Ingest(new(Guid.NewGuid(), sensorId, "device", 27, "C", DateTimeOffset.UtcNow, "ARDUINO"), default);
        Assert.IsType<BadRequestObjectResult>(response);
        Assert.Empty(db.Readings);
    }

    [Fact]
    public async Task RegisteredDeviceSensorIsExcludedFromManualAndBatchSimulation()
    {
        Guid sensorId = Guid.NewGuid();
        Mock<IMonitoringRepository> repository = new();
        repository.Setup(item => item.GetSimulationOverridesAsync(default)).ReturnsAsync(new Dictionary<Guid, decimal>());
        Mock<ISensorCatalogClient> catalog = new();
        catalog.Setup(client => client.GetActiveSensorsAsync(default)).ReturnsAsync([
            new SensorSummary(sensorId, Guid.NewGuid(), "Physical", "P1", SensorType.Temperature, "C", true)]);
        MonitoringService service = new(repository.Object, catalog.Object, Mock.Of<IAlertEvaluationClient>(), Mock.Of<IRealtimePublisher>(),
            Mock.Of<ISimulatedValueGenerator>(), new CreateReadingRequestValidator(), TimeProvider.System,
            Options.Create(new TelemetryOptions { Devices = [new() { DeviceId = "device", SensorId = sensorId }] }));
        Assert.False((await service.CreateAsync(new(sensorId, 20), default)).IsSuccess);
        await service.GenerateSimulationBatchAsync(default);
        repository.Verify(item => item.AddAsync(It.IsAny<Climate.Monitoring.Domain.Readings.SensorReading>(), default), Times.Never);
    }
}
