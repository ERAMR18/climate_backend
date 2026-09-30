using System.Net.Http.Headers;
using System.Text.Json;
using Climate.Monitoring.Application.Readings;
using Climate.Monitoring.Application.Simulation;
using Climate.Monitoring.Domain.Readings;
using Climate.Monitoring.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Climate.Monitoring.Api.Controllers;

[ApiController, Authorize, Route("api/v1/dashboard")]
public sealed class DashboardController(MonitoringDbContext db, IHttpClientFactory clients,
    IConfiguration configuration, ISimulationService simulation) : ControllerBase
{
    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummary>> Get([FromQuery] Guid? communityId, CancellationToken token)
    {
        string filter = communityId.HasValue ? $"communityId={communityId:D}&" : string.Empty;
        var communitiesTask = Fetch("SensorService", "api/v1/communities", token);
        var sensorsTask = Fetch("SensorService", $"api/v1/sensors?{filter}", token);
        var alertsTask = Fetch("AlertService", $"api/v1/alerts?{filter}isActive=true", token);
        var eventsTask = Fetch("EventService", $"api/v1/events/statistics?{filter}", token);
        try { await Task.WhenAll(communitiesTask, sensorsTask, alertsTask, eventsTask); }
        catch (HttpRequestException) { return Problem(statusCode: 503, title: "A dashboard dependency is unavailable."); }

        var communities = (await communitiesTask).EnumerateArray().ToArray();
        var sensors = (await sensorsTask).EnumerateArray().ToArray();
        var alerts = (await alertsTask).EnumerateArray().ToArray();
        IQueryable<SensorReading> query = db.Readings.AsNoTracking();
        if (communityId.HasValue) query = query.Where(x => x.CommunityId == communityId);
        var current = await query.GroupBy(x => x.SensorId)
            .Select(g => g.OrderByDescending(x => x.RecordedAt).First()).ToArrayAsync(token);
        var since = DateTimeOffset.UtcNow.AddHours(-24);
        var evolution = await query.Where(x => x.RecordedAt >= since)
            .GroupBy(x => new { x.SensorType, x.Unit, x.RecordedAt.Year, x.RecordedAt.Month, x.RecordedAt.Day, x.RecordedAt.Hour })
            .Select(g => new { g.Key.SensorType, g.Key.Unit, g.Key.Year, g.Key.Month, g.Key.Day, g.Key.Hour, Value = g.Average(x => x.Value) })
            .ToArrayAsync(token);
        var points = evolution.Select(x => new DashboardChartPoint(x.SensorType, x.Unit,
            new DateTimeOffset(x.Year, x.Month, x.Day, x.Hour, 0, 0, TimeSpan.Zero), x.Value))
            .OrderBy(x => x.Timestamp).ToArray();
        return Ok(new DashboardSummary(
            communities.Count(x => !communityId.HasValue || x.GetProperty("id").GetGuid() == communityId),
            sensors.Count(x => x.GetProperty("isActive").GetBoolean()),
            sensors.Count(x => !x.GetProperty("isActive").GetBoolean()),
            alerts.Count(x => x.GetProperty("status").GetString() == "Active"),
            alerts.GroupBy(x => x.GetProperty("level").GetString()!).ToDictionary(g => g.Key, g => g.Count()),
            communities, sensors, alerts, current.Select(SensorReadingResponse.FromEntity).ToArray(),
            simulation.GetStatus(), await eventsTask, points));
    }

    private async Task<JsonElement> Fetch(string service, string path, CancellationToken token)
    {
        var baseUrl = configuration[$"{service}:BaseUrl"] ?? throw new InvalidOperationException($"{service}:BaseUrl is required.");
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(baseUrl), path));
        request.Headers.Authorization = AuthenticationHeaderValue.Parse(Request.Headers.Authorization.ToString());
        using var client = clients.CreateClient("dashboard");
        using var response = await client.SendAsync(request, token);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>(token);
    }
}

public sealed record DashboardChartPoint(SensorType SensorType, string Unit, DateTimeOffset Timestamp, decimal Value);
public sealed record DashboardSummary(int CommunityCount, int ActiveSensorCount, int InactiveSensorCount,
    int ActiveAlertCount, IReadOnlyDictionary<string, int> ByAlertLevel,
    IReadOnlyCollection<JsonElement> Communities, IReadOnlyCollection<JsonElement> Sensors,
    IReadOnlyCollection<JsonElement> Alerts, IReadOnlyCollection<SensorReadingResponse> Readings,
    SimulationStatusResponse Simulation, JsonElement Events, IReadOnlyCollection<DashboardChartPoint> Evolution);
