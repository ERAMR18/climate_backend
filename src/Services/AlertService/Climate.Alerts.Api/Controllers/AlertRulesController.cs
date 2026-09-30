using Climate.Alerts.Domain.Alerts;
using Climate.Alerts.Infrastructure.Persistence;
using Climate.Contracts.Identity;
using Climate.Contracts.Sensors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Climate.Alerts.Api.Controllers;

[ApiController, Authorize, Route("api/v1/alert-rules")]
public sealed class AlertRulesController(AlertsDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyCollection<AlertRuleResponse>>> List(CancellationToken token) =>
        Ok((await db.AlertRules.AsNoTracking().OrderBy(x => x.Name).ToArrayAsync(token)).Select(AlertRuleResponse.FromEntity));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AlertRuleResponse>> Get(Guid id, CancellationToken token) =>
        await db.AlertRules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, token) is { } rule
            ? Ok(AlertRuleResponse.FromEntity(rule)) : NotFound();

    [HttpPost, Authorize(Roles = SystemRoles.Administrator)]
    public async Task<ActionResult<AlertRuleResponse>> Create(AlertRuleRequest request, CancellationToken token)
    {
        if (!request.IsValid()) return ValidationProblem("Name, message, valid enums and an ordered interval with at least one bound are required.");
        var rule = new AlertRule { Id = Guid.NewGuid(), CreatedAt = DateTimeOffset.UtcNow };
        request.Apply(rule); db.AlertRules.Add(rule); await db.SaveChangesAsync(token);
        return CreatedAtAction(nameof(Get), new { id = rule.Id }, AlertRuleResponse.FromEntity(rule));
    }

    [HttpPut("{id:guid}"), Authorize(Roles = SystemRoles.Administrator)]
    public async Task<ActionResult<AlertRuleResponse>> Update(Guid id, AlertRuleRequest request, CancellationToken token)
    {
        if (!request.IsValid()) return ValidationProblem("Invalid rule interval or fields.");
        var rule = await db.AlertRules.SingleOrDefaultAsync(x => x.Id == id, token);
        if (rule is null) return NotFound();
        request.Apply(rule); await db.SaveChangesAsync(token); return Ok(AlertRuleResponse.FromEntity(rule));
    }
    [HttpPatch("{id:guid}/activate"), Authorize(Roles = SystemRoles.Administrator)]
    public Task<ActionResult> Activate(Guid id, CancellationToken token) => SetStatus(id, true, token);
    [HttpPatch("{id:guid}/deactivate"), Authorize(Roles = SystemRoles.Administrator)]
    public Task<ActionResult> Deactivate(Guid id, CancellationToken token) => SetStatus(id, false, token);
    private async Task<ActionResult> SetStatus(Guid id, bool active, CancellationToken token)
    {
        var rule = await db.AlertRules.SingleOrDefaultAsync(x => x.Id == id, token);
        if (rule is null) return NotFound();
        rule.IsActive = active; rule.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(token); return NoContent();
    }
}
public sealed record AlertRuleRequest(string Name, SensorType SensorType, decimal? MinimumValue,
    decimal? MaximumValue, AlertLevel AlertLevel, RiskType RiskType, string Message, bool IsActive = true)
{
    public bool IsValid() => !string.IsNullOrWhiteSpace(Name) && Name.Length <= 200 &&
        !string.IsNullOrWhiteSpace(Message) && Message.Length <= 1000 && Enum.IsDefined(SensorType) &&
        Enum.IsDefined(AlertLevel) && Enum.IsDefined(RiskType) && (MinimumValue.HasValue || MaximumValue.HasValue) &&
        !(MinimumValue > MaximumValue) && (MinimumValue is null or >= -1000000m and <= 1000000m) &&
        (MaximumValue is null or >= -1000000m and <= 1000000m);
    public void Apply(AlertRule rule)
    {
        rule.Name = Name.Trim(); rule.SensorType = (int)SensorType; rule.MinimumValue = MinimumValue;
        rule.MaximumValue = MaximumValue; rule.AlertLevel = AlertLevel; rule.RiskType = RiskType;
        rule.Message = Message.Trim(); rule.IsActive = IsActive; rule.UpdatedAt = DateTimeOffset.UtcNow;
    }
}
public sealed record AlertRuleResponse(Guid Id, string Name, SensorType SensorType, decimal? MinimumValue,
    decimal? MaximumValue, AlertLevel AlertLevel, RiskType RiskType, string Message, bool IsActive,
    DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt)
{
    public static AlertRuleResponse FromEntity(AlertRule x) => new(x.Id, x.Name, (SensorType)x.SensorType,
        x.MinimumValue, x.MaximumValue, x.AlertLevel, x.RiskType, x.Message, x.IsActive, x.CreatedAt, x.UpdatedAt);
}
