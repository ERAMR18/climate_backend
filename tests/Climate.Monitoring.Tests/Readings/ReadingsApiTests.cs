using Climate.Contracts.Sensors;
using Climate.Monitoring.Api.Controllers;
using Climate.Monitoring.Application.Abstractions;
using Climate.Monitoring.Domain.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Moq;
using SensorType = Climate.Monitoring.Domain.Readings.SensorType;

namespace Climate.Monitoring.Tests.Readings;

public sealed class ReadingsApiTests
{
    [Fact]
    public async Task BulkReadingsFilterByCommunitySensorDatesAndPage()
    {
        await using var db = new MonitoringDbContext(new DbContextOptionsBuilder<MonitoringDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Guid sensor = Guid.NewGuid(), community = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        db.Readings.AddRange(
            SensorReading.Create(Guid.NewGuid(), sensor, community, SensorType.Smoke, 50m, "%", now),
            SensorReading.Create(Guid.NewGuid(), sensor, community, SensorType.Smoke, 60m, "%", now.AddMinutes(1)),
            SensorReading.Create(Guid.NewGuid(), Guid.NewGuid(), community, SensorType.Smoke, 99m, "%", now));
        await db.SaveChangesAsync();
        var controller = new ReadingsController(db, Mock.Of<ISensorCatalogClient>());
        var response = await controller.List(community, sensor, now, now.AddMinutes(1), 2, 1);
        var page = Assert.IsType<ReadingPage>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(2, page.Total);
        var reading = Assert.Single(page.Items);
        Assert.Equal(50m, reading.Value);
        Assert.True(reading.SensorWasActive);
    }

    [Fact]
    public async Task SimulatedValuePersistsAndCanReturnToRandomMode()
    {
        var options = new DbContextOptionsBuilder<MonitoringDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        Guid sensor = Guid.NewGuid();
        var catalog = new Mock<ISensorCatalogClient>();
        catalog.Setup(x => x.GetActiveSensorsAsync(default)).ReturnsAsync([
            new SensorSummary(sensor, Guid.NewGuid(), "Smoke", "S1", Climate.Contracts.Sensors.SensorType.Smoke, "%", true)]);
        await using (var db = new MonitoringDbContext(options))
        {
            var controller = new ReadingsController(db, catalog.Object);
            Assert.IsType<OkObjectResult>((await controller.SetValue(sensor, new(75m), default)).Result);
        }
        await using var reopened = new MonitoringDbContext(options);
        var api = new ReadingsController(reopened, catalog.Object);
        var value = Assert.IsType<SimulationValueResponse>(Assert.IsType<OkObjectResult>((await api.GetValue(sensor, default)).Result).Value);
        Assert.Equal(75m, value.Value);
        await api.ClearValue(sensor, default);
        Assert.Empty(reopened.SimulationOverrides);
        Assert.IsType<NotFoundResult>((await api.SetValue(Guid.NewGuid(), new(75m), default)).Result);
    }
}
