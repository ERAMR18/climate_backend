using Climate.Contracts.Alerts;
using Climate.Events.Api.Controllers;
using Climate.Events.Domain.Events;
using Climate.Events.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Climate.Events.Tests.Events;

public sealed class EventStatisticsTests
{
    [Fact]
    public async Task StatisticsApplyCommunityAndInclusiveDatesBeforeGrouping()
    {
        await using var db = new EventsDbContext(new DbContextOptionsBuilder<EventsDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        Guid community = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var first = Create(community, now, AlertLevel.Red);
        first.SetWorkflow(90m, "Attended", Guid.NewGuid());
        db.Events.AddRange(first, Create(community, now.AddHours(-1), AlertLevel.Yellow),
            Create(Guid.NewGuid(), now, AlertLevel.Red), Create(community, now.AddDays(-2), AlertLevel.Red));
        await db.SaveChangesAsync();
        var response = await new EventStatisticsController(db).Get(community, now.AddHours(-1), now, default);
        var stats = Assert.IsType<EventStatisticsResponse>(Assert.IsType<OkObjectResult>(response.Result).Value);
        Assert.Equal(2, stats.Total);
        Assert.Equal(2, stats.ByRiskType["Flood"]);
        Assert.Equal(1, stats.ByStatus["Attended"]);
        Assert.Equal(1, stats.ByAlertLevel["Yellow"]);
        var empty = await new EventStatisticsController(db).Get(Guid.NewGuid(), null, null, default);
        Assert.Equal(0, Assert.IsType<EventStatisticsResponse>(Assert.IsType<OkObjectResult>(empty.Result).Value).Total);
    }

    private static ClimateEvent Create(Guid community, DateTimeOffset now, AlertLevel level) =>
        ClimateEvent.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), community, RiskType.Flood, level, "Flood", now);
}
