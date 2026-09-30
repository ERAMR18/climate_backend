using Climate.Events.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Climate.Events.Api.Controllers;
[ApiController, Authorize, Route("api/v1/events/statistics")]
public sealed class EventStatisticsController(EventsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<EventStatisticsResponse>> Get([FromQuery] Guid? communityId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken token)
    {
        if (from > to) return ValidationProblem("Invalid date range.");
        var query = db.Events.AsNoTracking();
        if (communityId.HasValue) query = query.Where(x => x.CommunityId == communityId);
        if (from.HasValue) query = query.Where(x => x.OccurredAt >= from);
        if (to.HasValue) query = query.Where(x => x.OccurredAt <= to);
        var groups = await query.GroupBy(x => new { x.RiskType, x.AlertLevel, x.Status })
            .Select(g => new { g.Key.RiskType, g.Key.AlertLevel, g.Key.Status, Count = g.Count() }).ToArrayAsync(token);
        return Ok(new EventStatisticsResponse(groups.Sum(x => x.Count),
            groups.GroupBy(x => x.RiskType.ToString()).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)),
            groups.GroupBy(x => x.AlertLevel.ToString()).ToDictionary(g => g.Key, g => g.Sum(x => x.Count)),
            groups.GroupBy(x => x.Status).ToDictionary(g => g.Key, g => g.Sum(x => x.Count))));
    }
}
public sealed record EventStatisticsResponse(int Total, IReadOnlyDictionary<string, int> ByRiskType,
    IReadOnlyDictionary<string, int> ByAlertLevel, IReadOnlyDictionary<string, int> ByStatus);
